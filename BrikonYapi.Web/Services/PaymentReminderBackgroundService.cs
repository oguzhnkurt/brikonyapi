using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// Günde bir kez çalışan arka plan görevi:
    ///   1) "PaymentNotifications:ReminderCheckpoints" içinde tanımlı her kontrol noktası için
    ///      (varsayılan: vadeye 7 gün ve 1 gün kala) henüz ödenmemiş taksitlere, o kontrol noktası
    ///      için daha önce gönderilmediyse hatırlatma yollar (SMS/e-posta + yapılandırılmışsa WhatsApp).
    ///   2) Vadesi geçmiş ama hâlâ "Pending" görünen taksitleri "Overdue" olarak işaretler ve
    ///      (daha önce bildirilmediyse) malike gecikme bildirimi gönderir.
    ///
    /// "Daha önce gönderildi mi?" kontrolü ayrı bir alan eklemek yerine NotificationLog'daki
    /// kayıtlar üzerinden yapılır (her kontrol noktası kendi Subject'iyle tekilleşir) — bu sayede
    /// şema değişikliği gerekmez.
    /// </summary>
    public class PaymentReminderBackgroundService : BackgroundService
    {
        private const string OverdueSubject = "Gecikmiş Ödeme — Brikon Yapı";
        private static readonly int[] DefaultReminderCheckpoints = { 7, 1 };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentReminderBackgroundService> _logger;

        public PaymentReminderBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<PaymentReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Uygulama her açıldığında hemen bir kontrol yapılır (admin test edebilsin diye),
            // sonrasında günde bir kez tekrarlanır.
            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

            await RunOnceAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var notify = scope.ServiceProvider.GetRequiredService<PaymentNotificationService>();

                var checkpoints = _config.GetSection("PaymentNotifications:ReminderCheckpoints").Get<int[]>();
                if (checkpoints == null || checkpoints.Length == 0) checkpoints = DefaultReminderCheckpoints;
                // Büyükten küçüğe sırala: aynı gün birden fazla kontrol noktasına birden denk gelinirse
                // (ör. uygulama birkaç gün kapalı kalmışsa) en erken/az acil olandan başlanır.
                checkpoints = checkpoints.OrderByDescending(d => d).ToArray();

                var today = DateTime.Today;

                var pending = await db.PaymentSchedules
                    .Include(s => s.Unit).ThenInclude(u => u!.Owner)
                    .Where(s => s.Status == PaymentScheduleStatus.Pending)
                    .ToListAsync(ct);

                foreach (var schedule in pending)
                {
                    var owner = schedule.Unit?.Owner;
                    if (owner == null) continue;

                    if (schedule.DueDate.Date < today)
                    {
                        // Vade geçti → Overdue işaretle + (ilk kezse) bildir.
                        schedule.Status = PaymentScheduleStatus.Overdue;
                        schedule.UpdatedAt = DateTime.Now;

                        var alreadyNotified = await db.NotificationLogs.AnyAsync(
                            n => n.RelatedPaymentScheduleId == schedule.Id && n.Subject == OverdueSubject, ct);
                        if (!alreadyNotified)
                            await notify.NotifyOverdueAsync(owner, schedule);
                        continue;
                    }

                    // Ulaşılmış her kontrol noktası için (vade o noktaya eşit veya daha yakınsa),
                    // o noktaya özgü Subject'le daha önce gönderilmediyse hatırlatma yollanır.
                    foreach (var daysBefore in checkpoints)
                    {
                        if (schedule.DueDate.Date > today.AddDays(daysBefore)) continue;

                        var subject = $"Ödeme Hatırlatması ({daysBefore} gün kala) — Brikon Yapı";
                        var alreadyReminded = await db.NotificationLogs.AnyAsync(
                            n => n.RelatedPaymentScheduleId == schedule.Id && n.Subject == subject, ct);
                        if (!alreadyReminded)
                            await notify.NotifyReminderAsync(owner, schedule, daysBefore);
                    }
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ödeme hatırlatma görevi çalışırken hata oluştu.");
            }
        }
    }
}
