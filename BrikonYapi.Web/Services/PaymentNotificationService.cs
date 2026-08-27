using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// Ödeme kalemleriyle ilgili malik bildirimlerini (yeni taksit, vade hatırlatma, gecikme,
    /// ödeme onayı/reddi) malikin tercih ettiği kanallardan (SMS/e-posta) gönderir ve her
    /// denemeyi NotificationLog'a kaydeder. Sağlayıcı yapılandırılmamışsa (SmsService/EmailService
    /// no-op döner) yine de bir "Failed" log satırı bırakır ki admin panelinde görülebilsin.
    /// </summary>
    public class PaymentNotificationService
    {
        private readonly AppDbContext _db;
        private readonly SmsService _sms;
        private readonly EmailService _email;
        private readonly WhatsAppService _whatsapp;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentNotificationService> _logger;

        public PaymentNotificationService(AppDbContext db, SmsService sms, EmailService email, WhatsAppService whatsapp, IConfiguration config, ILogger<PaymentNotificationService> logger)
        {
            _db = db;
            _sms = sms;
            _email = email;
            _whatsapp = whatsapp;
            _config = config;
            _logger = logger;
        }

        /// <summary>Bir WhatsApp bildirimi için gönderilecek onaylı şablon adı ve gövde parametreleri.
        /// null geçilirse (çoğu bildirim tipi — henüz onaylı şablonu olmayanlar) WhatsApp atlanır;
        /// SMS/e-posta serbest metinle gönderilmeye devam eder.</summary>
        private sealed record WhatsAppTemplate(string Name, IReadOnlyList<string> BodyParams);

        private static string Money(PaymentSchedule schedule) =>
            schedule.CurrencySymbol + schedule.Amount.ToString("N0", new System.Globalization.CultureInfo("tr-TR"));

        private async Task<OwnerNotificationPreference> GetPreferenceAsync(int ownerId)
        {
            var pref = await _db.OwnerNotificationPreferences.FirstOrDefaultAsync(p => p.OwnerId == ownerId);
            // Tercih hiç oluşturulmamışsa entity'nin varsayılanları (hepsi açık) geçerli sayılır.
            return pref ?? new OwnerNotificationPreference { OwnerId = ownerId };
        }

        /// <summary>Bir malike, bir ödeme kalemiyle ilgili bildirim gönderir (tercihlerine saygılı) ve
        /// gönderim denemelerini NotificationLog'a yazar.</summary>
        private async Task NotifyOwnerAsync(Owner owner, PaymentSchedule? schedule, string subject, string message, WhatsAppTemplate? whatsapp = null)
        {
            var pref = await GetPreferenceAsync(owner.Id);
            if (!pref.NotifyPayment) return; // malik ödeme bildirimlerini kapatmış

            if (pref.WhatsAppEnabled && whatsapp != null && !string.IsNullOrWhiteSpace(owner.Phone))
            {
                var languageCode = _config["WhatsApp:TemplateLanguage"] ?? "tr";
                var (ok, err) = await _whatsapp.SendTemplateAsync(owner.Phone!, whatsapp.Name, languageCode, whatsapp.BodyParams);
                _db.NotificationLogs.Add(new NotificationLog
                {
                    OwnerId = owner.Id,
                    RelatedPaymentScheduleId = schedule?.Id,
                    Channel = NotificationChannel.WhatsApp,
                    Subject = subject,
                    Message = message,
                    Status = ok ? NotificationStatus.Sent : NotificationStatus.Failed,
                    ErrorMessage = err,
                    SentAt = ok ? DateTime.Now : null
                });
            }

            if (pref.SmsEnabled && !string.IsNullOrWhiteSpace(owner.Phone))
            {
                var (ok, err) = await _sms.SendAsync(owner.Phone!, message);
                _db.NotificationLogs.Add(new NotificationLog
                {
                    OwnerId = owner.Id,
                    RelatedPaymentScheduleId = schedule?.Id,
                    Channel = NotificationChannel.Sms,
                    Subject = subject,
                    Message = message,
                    Status = ok ? NotificationStatus.Sent : NotificationStatus.Failed,
                    ErrorMessage = err,
                    SentAt = ok ? DateTime.Now : null
                });
            }

            if (pref.EmailEnabled && !string.IsNullOrWhiteSpace(owner.Email))
            {
                var html = $"""
                    <html><body style="font-family:Arial,sans-serif;color:#222;">
                    <p>Merhaba {System.Web.HttpUtility.HtmlEncode(owner.FullName)},</p>
                    <p>{System.Web.HttpUtility.HtmlEncode(message)}</p>
                    <p style="margin-top:16px;font-size:.85rem;color:#888;">
                        Detaylar için Kat Maliki Portalı → Ödemelerim ekranını ziyaret edebilirsiniz.
                    </p>
                    </body></html>
                    """;
                var (ok, err) = await _email.SendAsync(owner.Email!, owner.FullName, subject, html);
                _db.NotificationLogs.Add(new NotificationLog
                {
                    OwnerId = owner.Id,
                    RelatedPaymentScheduleId = schedule?.Id,
                    Channel = NotificationChannel.Email,
                    Subject = subject,
                    Message = message,
                    Status = ok ? NotificationStatus.Sent : NotificationStatus.Failed,
                    ErrorMessage = err,
                    SentAt = ok ? DateTime.Now : null
                });
            }

            await _db.SaveChangesAsync();
        }

        public Task NotifyNewScheduleAsync(Owner owner, PaymentSchedule schedule)
        {
            var desc = string.IsNullOrWhiteSpace(schedule.Description) ? "Yeni ödeme kalemi" : schedule.Description;
            var message = $"{desc} için {Money(schedule)} tutarında yeni bir ödeme kalemi tanımlandı. Vade: {schedule.DueDate:dd.MM.yyyy}.";
            return NotifyOwnerAsync(owner, schedule, "Yeni Ödeme Kalemi — Brikon Yapı", message);
        }

        /// <summary>Vade hatırlatması gönderir. <paramref name="daysBefore"/>, hatırlatmanın hangi
        /// kontrol noktasına ait olduğunu belirtir (ör. 7 = "1 hafta kala", 1 = "1 gün kala") ve
        /// bildirim konusuna dahil edilir — böylece PaymentReminderBackgroundService'in "bu kontrol
        /// noktası için daha önce gönderildi mi?" kontrolü (NotificationLog.Subject üzerinden) her
        /// kontrol noktasını birbirinden bağımsız olarak tekilleştirebilir.</summary>
        public Task NotifyReminderAsync(Owner owner, PaymentSchedule schedule, int daysBefore)
        {
            var desc = string.IsNullOrWhiteSpace(schedule.Description) ? "Taksit" : schedule.Description;
            var whenText = daysBefore switch
            {
                <= 0 => "bugün",
                1 => "yarın",
                7 => "1 hafta içinde",
                _ => $"{daysBefore} gün içinde"
            };
            var message = $"{desc} taksitinizin vadesi {whenText} ({schedule.DueDate:dd.MM.yyyy}) doluyor. Tutar: {Money(schedule)}.";
            var subject = $"Ödeme Hatırlatması ({daysBefore} gün kala) — Brikon Yapı";

            // WhatsApp:ReminderTemplateName appsettings'te tanımlıysa (hesap açılıp şablon Meta'da
            // onaylandıktan sonra) aynı hatırlatma WhatsApp üzerinden de gönderilir. Şablon parametreleri:
            // {{1}} malik adı, {{2}} tutar, {{3}} vade tarihi — 360dialog/Meta'da bu sırayla onaylatılmalı.
            var templateName = _config["WhatsApp:ReminderTemplateName"];
            WhatsAppTemplate? whatsapp = string.IsNullOrWhiteSpace(templateName)
                ? null
                : new WhatsAppTemplate(templateName, new[] { owner.FullName, Money(schedule), schedule.DueDate.ToString("dd.MM.yyyy") });

            return NotifyOwnerAsync(owner, schedule, subject, message, whatsapp);
        }

        /// <summary>Bir inşaat aşaması "Tamamlandı" olarak işaretlendiğinde, o aşamaya bağlı ve henüz
        /// ödenmemiş taksidi olan malike bilgilendirme gönderir.</summary>
        public Task NotifyStageReachedAsync(Owner owner, PaymentSchedule schedule, ProjectStage stage)
        {
            var desc = string.IsNullOrWhiteSpace(schedule.Description) ? "Hakediş taksitiniz" : schedule.Description;
            var message = $"\"{stage.Name}\" aşaması tamamlandı. {desc} ({Money(schedule)}) ödemeye hazır.";
            return NotifyOwnerAsync(owner, schedule, "İlerleme Tamamlandı — Brikon Yapı", message);
        }

        public Task NotifyOverdueAsync(Owner owner, PaymentSchedule schedule)
        {
            var desc = string.IsNullOrWhiteSpace(schedule.Description) ? "Taksit" : schedule.Description;
            var message = $"{desc} taksitinizin vadesi geçti ({schedule.DueDate:dd.MM.yyyy}). Tutar: {Money(schedule)}. Lütfen en kısa sürede ödeme yapın.";
            return NotifyOwnerAsync(owner, schedule, "Gecikmiş Ödeme — Brikon Yapı", message);
        }

        public Task NotifyTransactionApprovedAsync(Owner owner, PaymentSchedule schedule)
        {
            var desc = string.IsNullOrWhiteSpace(schedule.Description) ? "Taksit" : schedule.Description;
            var message = $"{desc} ({Money(schedule)}) ödemeniz onaylandı. Teşekkür ederiz.";
            return NotifyOwnerAsync(owner, schedule, "Ödemeniz Onaylandı — Brikon Yapı", message);
        }

        public Task NotifyTransactionRejectedAsync(Owner owner, PaymentSchedule schedule, string? note)
        {
            var desc = string.IsNullOrWhiteSpace(schedule.Description) ? "Taksit" : schedule.Description;
            var reason = string.IsNullOrWhiteSpace(note) ? "" : $" Sebep: {note}.";
            var message = $"{desc} için gönderdiğiniz ödeme bildirimi onaylanmadı.{reason} Lütfen tekrar deneyin veya yönetimle iletişime geçin.";
            return NotifyOwnerAsync(owner, schedule, "Ödeme Bildirimi Onaylanmadı — Brikon Yapı", message);
        }

        /// <summary>Malik bir dekont yüklediğinde admin'e (Smtp:NotifyEmail) bilgi e-postası gönderir.
        /// Bu bir "malik bildirimi" değil, dahili bir admin uyarısıdır — OwnerNotificationPreference'a bağlı değildir.</summary>
        public async Task NotifyAdminReceiptUploadedAsync(Owner owner, PaymentSchedule schedule)
        {
            try
            {
                var html = $"""
                    <html><body style="font-family:Arial,sans-serif;color:#222;">
                    <h3>Yeni Dekont Bildirimi</h3>
                    <p><strong>{System.Web.HttpUtility.HtmlEncode(owner.FullName)}</strong>,
                    <strong>{System.Web.HttpUtility.HtmlEncode(schedule.Description ?? "taksit")}</strong>
                    ({Money(schedule)}) için bir havale/EFT dekontu yükledi.</p>
                    <p style="margin-top:16px;font-size:.85rem;color:#888;">
                        Admin Panel → Ödeme Planları üzerinden onaylayabilir veya reddedebilirsiniz.
                    </p>
                    </body></html>
                    """;
                var toAddr = _config["Smtp:NotifyEmail"] ?? "info@brikonyapi.com";
                await _email.SendAsync(toAddr, "Brikon Yapı Yönetim", "Yeni Dekont Bildirimi — Brikon Yapı", html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin dekont bildirimi gönderilemedi.");
            }
        }
    }
}
