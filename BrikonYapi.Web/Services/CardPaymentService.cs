using System.Security.Cryptography;
using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Services
{
    public enum CardPaymentOutcome { Completed, Pending, Failed, NeedsReview }

    /// <summary>
    /// Kredi kartı ile taksit ödemesi (TKPAY Ödeme Formu) iş akışı:
    ///  1) StartAsync: Initiated durumunda bir PaymentTransaction açar, TKPAY ödeme formunu oluşturur.
    ///  2) VerifyAsync: TKPAY sorgu servisiyle sonucu doğrular; başarılıysa taksiti "Ödendi" yapar.
    /// Sonuç kararı HİÇBİR ZAMAN tarayıcıdan gelen yönlendirme verisine göre verilmez — yalnızca
    /// sunucudan sunucuya yapılan inquiry sorgusuna göre verilir (sahte "başarılı" POST'a karşı koruma).
    /// </summary>
    public class CardPaymentService
    {
        private readonly AppDbContext _db;
        private readonly TkpayClient _tkpay;
        private readonly PaymentNotificationService _notify;
        private readonly IConfiguration _config;
        private readonly ILogger<CardPaymentService> _logger;

        public CardPaymentService(AppDbContext db, TkpayClient tkpay, PaymentNotificationService notify,
                                  IConfiguration config, ILogger<CardPaymentService> logger)
        {
            _db = db;
            _tkpay = tkpay;
            _notify = notify;
            _config = config;
            _logger = logger;
        }

        public bool IsAvailable => _tkpay.IsConfigured;

        /// <summary>Teklif yalnızca yurt içi kartlarla TL işlemler için geçerli; döviz taksitler havale ile ödenir.</summary>
        public static bool SupportsCurrency(PaymentCurrency c) => c == PaymentCurrency.TRY;

        private string PublicBase => ((_config["Tkpay:PublicBaseUrl"] ?? "").Trim() is { Length: > 0 } u ? u : "https://brikonyapi.com").TrimEnd('/');

        /// <summary>TKPAY'e gönderilen tekil sipariş numarası: "BK" + 18 karakter (toplam 20, TKPAY sınırı).</summary>
        private static string NewExternalId()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = RandomNumberGenerator.GetBytes(18);
            return "BK" + new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
        }

        /// <summary>Ödeme formunu başlatır. Başarılıysa malikin yönlendirileceği TKPAY adresini döner.</summary>
        public async Task<(string? RedirectUrl, string? Error)> StartAsync(Owner owner, PaymentSchedule schedule, CancellationToken ct = default)
        {
            if (!IsAvailable) return (null, "Kartla ödeme henüz aktif değil.");
            if (!SupportsCurrency(schedule.Currency)) return (null, "Döviz cinsinden taksitler için kartla ödeme desteklenmiyor; lütfen havale/EFT ile ödeyin.");
            if (schedule.Status == PaymentScheduleStatus.Paid || schedule.Status == PaymentScheduleStatus.Cancelled)
                return (null, "Bu taksit için ödeme alınamaz.");
            if (schedule.Amount <= 0) return (null, "Geçersiz tutar.");

            // Bekleyen bir havale bildirimi varsa ikinci kez ödeme alınmasın
            var txs = await _db.PaymentTransactions.Where(t => t.PaymentScheduleId == schedule.Id).ToListAsync(ct);
            if (txs.Any(t => t.Method == PaymentMethod.BankTransfer && t.Status == PaymentTransactionStatus.PendingApproval))
                return (null, "Bu taksit için incelenmekte olan bir havale bildiriminiz var.");

            // Daha önce açılmış ama sonucu bilinmeyen kart denemeleri varsa önce onları doğrula —
            // malik ödemeyi yapıp sayfayı kapatmış olabilir; çift tahsilatın önüne geçer.
            foreach (var open in txs.Where(t => t.Method == PaymentMethod.CreditCard && t.Status == PaymentTransactionStatus.Initiated))
            {
                var outcome = await VerifyAsync(open, ct);
                if (outcome == CardPaymentOutcome.Completed || outcome == CardPaymentOutcome.NeedsReview)
                    return (null, "Bu taksit için kartla yapılmış bir ödemeniz zaten alınmış görünüyor.");
                if (outcome == CardPaymentOutcome.Pending && open.CreatedAt > DateTime.Now.AddMinutes(-_tkpay.LinkExpireMinutes - 5))
                    return (null, "Bu taksit için başlatılmış bir kart ödemesi henüz sonuçlanmadı. Birkaç dakika sonra tekrar deneyin.");
            }

            var tx = new PaymentTransaction
            {
                PaymentScheduleId = schedule.Id,
                Method = PaymentMethod.CreditCard,
                Amount = schedule.Amount,
                Status = PaymentTransactionStatus.Initiated,
                ExternalId = NewExternalId(),
                CreatedAt = DateTime.Now
            };
            _db.PaymentTransactions.Add(tx);
            await _db.SaveChangesAsync(ct);

            var returnUrl = $"{PublicBase}/odeme/tkpay/donus";
            var desc = string.IsNullOrWhiteSpace(schedule.Description) ? "Taksit ödemesi" : schedule.Description!;
            var phone = NormalizePhone(owner.Phone);

            var result = await _tkpay.CreateCheckoutAsync(
                tx.ExternalId!, schedule.Amount, "TRY", returnUrl, returnUrl,
                string.IsNullOrWhiteSpace(owner.Email) ? null : owner.Email.Trim(), phone,
                $"Brikon Yapı · {desc}", $"TAKSIT-{schedule.Id}", ct);

            if (!result.Success)
            {
                tx.Status = PaymentTransactionStatus.Failed;
                tx.Note = result.Error;
                await _db.SaveChangesAsync(ct);
                return (null, result.Error ?? "Ödeme sayfası oluşturulamadı.");
            }

            tx.ProviderTransactionId = result.Token;
            await _db.SaveChangesAsync(ct);
            return (result.LinkUrl, null);
        }

        private static string? NormalizePhone(string? phone)
        {
            var d = new string((phone ?? "").Where(char.IsDigit).ToArray());
            if (d.Length == 10) d = "90" + d;
            else if (d.Length == 11 && d.StartsWith('0')) d = "9" + d;
            return d.Length == 12 && d.StartsWith("90") ? "+" + d : null;
        }

        /// <summary>
        /// Bir kart denemesinin sonucunu TKPAY'den sorgular ve kaydı günceller (idempotent).
        /// Başarılıysa taksit "Ödendi" olur ve malike/yönetime bildirim gider.
        /// </summary>
        public async Task<CardPaymentOutcome> VerifyAsync(PaymentTransaction tx, CancellationToken ct = default)
        {
            if (tx.Method != PaymentMethod.CreditCard || string.IsNullOrEmpty(tx.ExternalId))
                return CardPaymentOutcome.Failed;
            if (tx.Status == PaymentTransactionStatus.Completed) return CardPaymentOutcome.Completed;
            if (tx.Status == PaymentTransactionStatus.PendingApproval) return CardPaymentOutcome.NeedsReview;

            var inq = await _tkpay.InquireCheckoutAsync(tx.ExternalId, ct);
            if (!inq.CallSucceeded)
                return tx.Status == PaymentTransactionStatus.Failed ? CardPaymentOutcome.Failed : CardPaymentOutcome.Pending;

            if (inq.IsSuccess)
            {
                // Aynı anda iki doğrulama (dönüş sayfası + arka plan işi) çalışmış olabilir — tekrar bildirim gitmesin
                await _db.Entry(tx).ReloadAsync(ct);
                if (tx.Status == PaymentTransactionStatus.Completed) return CardPaymentOutcome.Completed;

                var schedule = await _db.PaymentSchedules
                    .Include(s => s.Unit).ThenInclude(u => u!.Owner)
                    .FirstAsync(s => s.Id == tx.PaymentScheduleId, ct);

                tx.ChargedAmount = inq.Amount;
                tx.InstallmentCount = inq.InstallmentCount;
                tx.InstallmentInterestAmount = inq.InstallmentInterestAmount
                    ?? (inq.Amount.HasValue && inq.BaseAmount.HasValue ? inq.Amount - inq.BaseAmount : null);
                tx.CardBin = inq.Bin is { Length: > 0 } b ? (b.Length > 8 ? b[..8] : b) : null;
                if (!string.IsNullOrEmpty(inq.OrderId)) tx.ProviderTransactionId = inq.OrderId;

                // Tutar/para birimi kontrolü: vade farksız tutar bizim kaydımızla birebir eşleşmeli
                var paidBase = inq.BaseAmount ?? inq.Amount ?? 0m;
                var currencyOk = string.IsNullOrEmpty(inq.Currency) || string.Equals(inq.Currency, "TRY", StringComparison.OrdinalIgnoreCase);
                if (!currencyOk || Math.Abs(paidBase - tx.Amount) > 0.01m)
                {
                    tx.Status = PaymentTransactionStatus.PendingApproval;
                    tx.Note = $"Kart ödemesi alındı ancak tutar eşleşmedi (beklenen {TkpayClient.Money(tx.Amount)}, gelen {TkpayClient.Money(paidBase)} {inq.Currency}). Lütfen kontrol edin.";
                    await _db.SaveChangesAsync(ct);
                    _logger.LogError("TKPAY tutar uyuşmazlığı: tx {TxId} ext {Ext}", tx.Id, tx.ExternalId);
                    return CardPaymentOutcome.NeedsReview;
                }

                tx.Status = PaymentTransactionStatus.Completed;
                tx.ApprovedAt = DateTime.Now;
                tx.ApprovedByUserId = "TKPAY";
                tx.Note = null;
                schedule.Status = PaymentScheduleStatus.Paid;
                schedule.PaidAt = DateTime.Now;
                schedule.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync(ct);

                var owner = schedule.Unit?.Owner;
                if (owner != null)
                {
                    try { await _notify.NotifyTransactionApprovedAsync(owner, schedule); }
                    catch (Exception ex) { _logger.LogError(ex, "Kart ödemesi bildirimi gönderilemedi (tx {TxId}).", tx.Id); }
                }
                return CardPaymentOutcome.Completed;
            }

            if (inq.IsError)
            {
                if (tx.Status != PaymentTransactionStatus.Failed)
                {
                    tx.Status = PaymentTransactionStatus.Failed;
                    tx.Note ??= "Ödeme banka tarafından onaylanmadı.";
                    await _db.SaveChangesAsync(ct);
                }
                return CardPaymentOutcome.Failed;
            }

            // Pending / form henüz kullanılmadı: süresi çoktan dolduysa denemeyi kapat
            if (tx.Status == PaymentTransactionStatus.Initiated && tx.CreatedAt < DateTime.Now.AddHours(-24))
            {
                tx.Status = PaymentTransactionStatus.Failed;
                tx.Note = "Ödeme tamamlanmadı (süre doldu).";
                await _db.SaveChangesAsync(ct);
                return CardPaymentOutcome.Failed;
            }
            return CardPaymentOutcome.Pending;
        }

        /// <summary>Arka plan işi: sonucu bilinmeyen (ve yakın zamanda başarısız sayılmış) kart denemelerini yeniden doğrular.
        /// Malik ödemeyi yapıp tarayıcıyı kapatsa bile taksit "Ödendi"ye döner.</summary>
        public async Task ReconcileOpenAsync(CancellationToken ct = default)
        {
            if (!IsAvailable) return;
            var since = DateTime.Now.AddHours(-48);
            var recentFail = DateTime.Now.AddHours(-3);
            var open = await _db.PaymentTransactions
                .Where(t => t.Method == PaymentMethod.CreditCard && t.ExternalId != null
                            && ((t.Status == PaymentTransactionStatus.Initiated && t.CreatedAt > since)
                                || (t.Status == PaymentTransactionStatus.Failed && t.CreatedAt > recentFail)))
                .OrderBy(t => t.CreatedAt)
                .Take(50)
                .ToListAsync(ct);

            foreach (var tx in open)
            {
                try { await VerifyAsync(tx, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Kart ödemesi doğrulanamadı (tx {TxId}).", tx.Id); }
            }
        }
    }

    /// <summary>Her 10 dakikada bir açık kart ödemelerini TKPAY'den doğrular.</summary>
    public class CardPaymentReconcileBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<CardPaymentReconcileBackgroundService> _logger;

        public CardPaymentReconcileBackgroundService(IServiceScopeFactory scopes, ILogger<CardPaymentReconcileBackgroundService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<CardPaymentService>().ReconcileOpenAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Kart ödemesi mutabakat işi hata verdi.");
                }
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}
