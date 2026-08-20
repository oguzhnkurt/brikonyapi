using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// Günde bir kez çalışan arka plan görevi:
    ///   1) Vadesine "PaymentNotifications:ReminderDaysBefore" gün veya daha az kalan, henüz
    ///      ödenmemiş taksitler için (daha önce hatırlatma gönderilmediyse) hatırlatma yollar.
    ///   2) Vadesi geçmiş ama hâlâ "Pending" görünen taksitleri "Overdue" olarak işaretler ve
    ///      (daha önce bildirilmediyse) malike gecikme bildirimi gönderir.
    ///
    /// "Daha önce gönderildi mi?" kontrolü ayrı bir alan eklemek yerine NotificationLog'daki
    /// kayıtlar üzerinden yapılır — bu sayede şema değişikliği gerekmez.
    /// </summary>
    public class PaymentReminderBackgroundService : BackgroundService
    {
        private const string ReminderSubject = "Ödeme Hatırlatması — Brikon Yapı";
        private const string OverdueSubject = "Gecikmiş Ödeme — Brikon Yapı";

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

                var reminderDays = _config.GetValue<int>("PaymentNotifications:ReminderDaysBefore", 3);
                var today = DateTime.Today;
                var reminderHorizon = today.AddDays(reminderDays);

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
                    }
                    else if (schedule.DueDate.Date <= reminderHorizon)
                    {
                        // Vadeye az kaldı → (ilk kezse) hatırlatma gönder.
                        var alreadyReminded = await db.NotificationLogs.AnyAsync(
                            n => n.RelatedPaymentScheduleId == schedule.Id && n.Subject == ReminderSubject, ct);
                        if (!alreadyReminded)
                            await notify.NotifyReminderAsync(owner, schedule);
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
