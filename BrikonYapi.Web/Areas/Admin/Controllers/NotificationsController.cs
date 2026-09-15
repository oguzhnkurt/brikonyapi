using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>
    /// Kat maliklerine toplu/manuel bildirim (şu an yalnızca WhatsApp) gönderme, gönderim geçmişini
    /// (otomatik hatırlatmalar dahil, NotificationLog üzerinden) listeleme ve otomatik ödeme
    /// hatırlatıcısının kaç gün önceden tetikleneceğini (SiteSetting: "ReminderCheckpoints") yönetme ekranı.
    /// Daha önce Kat Malikleri sayfasında satır seçim + yeşil buton olarak duran manuel gönderim
    /// buradan tamamen ayrı, kendi başına bir ekrana taşınmıştır.
    /// </summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class NotificationsController : Controller
    {
        private const string ReminderCheckpointsKey = "ReminderCheckpoints";
        private const string ReminderMessageTemplateKey = "ReminderMessageTemplate";
        private static readonly int[] AllowedPageSizes = { 25, 50, 100 };

        private readonly AppDbContext _db;
        private readonly WhatsAppService _whatsApp;
        private readonly SiteSettingService _settings;
        private readonly IConfiguration _config;

        public NotificationsController(AppDbContext db, WhatsAppService whatsApp, SiteSettingService settings, IConfiguration config)
        {
            _db       = db;
            _whatsApp = whatsApp;
            _settings = settings;
            _config   = config;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 25)
        {
            if (!AllowedPageSizes.Contains(pageSize)) pageSize = 25;
            if (page < 1) page = 1;

            var owners = await _db.Owners
                .Where(o => o.IsActive)
                .Include(o => o.Units).ThenInclude(u => u.Project)
                .OrderBy(o => o.FullName)
                .ToListAsync();

            ViewBag.NotificationPrefs = await _db.OwnerNotificationPreferences.ToListAsync();

            var historyQuery = _db.NotificationLogs.Include(n => n.Owner).OrderByDescending(n => n.CreatedAt);
            var totalCount = await historyQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var history = await historyQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.History = history;
            ViewBag.HistoryPage = page;
            ViewBag.HistoryPageSize = pageSize;
            ViewBag.HistoryTotalPages = totalPages;
            ViewBag.HistoryTotalCount = totalCount;

            var checkpointsRaw = await _settings.GetAsync(ReminderCheckpointsKey);
            ViewBag.ReminderCheckpoints = string.IsNullOrWhiteSpace(checkpointsRaw) ? "7,1" : checkpointsRaw;

            var templateRaw = await _settings.GetAsync(ReminderMessageTemplateKey);
            ViewBag.ReminderMessageTemplate = string.IsNullOrWhiteSpace(templateRaw) ? PaymentNotificationService.DefaultReminderMessageTemplate : templateRaw;

            ViewBag.WhatsAppConfigured = _whatsApp.IsConfigured && !string.IsNullOrWhiteSpace(_config["WhatsApp:ManualTemplateName"]);

            return View(owners);
        }

        /// <summary>
        /// Seçilen kat maliklerine onaylı bir WhatsApp şablonu üzerinden ("Sayın {{1}}, {{2}}")
        /// tek seferlik bildirim gönderir. Telefonu olmayan ya da WhatsApp bildirimi kapalı olan
        /// malikler atlanır, sonuç TempData ile raporlanır, her gönderim NotificationLog'a yazılır.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SendWhatsApp(List<int> ownerIds, string message)
        {
            if (ownerIds == null || ownerIds.Count == 0)
            {
                TempData["Error"] = "Lütfen en az bir kat maliki seçin.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["Error"] = "Mesaj metni boş olamaz.";
                return RedirectToAction(nameof(Index));
            }

            var templateName = _config["WhatsApp:ManualTemplateName"];
            if (string.IsNullOrWhiteSpace(templateName))
            {
                TempData["Error"] = "WhatsApp manuel gönderim şablonu henüz yapılandırılmamış (WhatsApp:ManualTemplateName).";
                return RedirectToAction(nameof(Index));
            }

            var languageCode = _config["WhatsApp:TemplateLanguage"] ?? "tr";
            var owners = await _db.Owners.Where(o => ownerIds.Contains(o.Id)).ToListAsync();
            var prefsById = await _db.OwnerNotificationPreferences
                .Where(p => ownerIds.Contains(p.OwnerId))
                .ToDictionaryAsync(p => p.OwnerId);

            var sent = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var owner in owners)
            {
                if (string.IsNullOrWhiteSpace(owner.Phone))
                {
                    skipped++;
                    continue;
                }

                if (prefsById.TryGetValue(owner.Id, out var pref) && !pref.WhatsAppEnabled)
                {
                    skipped++;
                    continue;
                }

                var (success, error) = await _whatsApp.SendTemplateAsync(
                    owner.Phone!, templateName, languageCode, new[] { owner.FullName, message.Trim() });

                _db.NotificationLogs.Add(new NotificationLog
                {
                    OwnerId      = owner.Id,
                    Channel      = NotificationChannel.WhatsApp,
                    Subject      = "Manuel bildirim (Admin)",
                    Message      = message.Trim(),
                    Status       = success ? NotificationStatus.Sent : NotificationStatus.Failed,
                    ErrorMessage = error,
                    SentAt       = success ? DateTime.Now : null,
                    CreatedAt    = DateTime.Now
                });

                if (success) sent++; else failed++;
            }

            await _db.SaveChangesAsync();

            var summary = $"{sent} kişiye gönderildi.";
            if (failed  > 0) summary += $" {failed} kişide hata oluştu.";
            if (skipped > 0) summary += $" {skipped} kişide telefon numarası kayıtlı değil ya da WhatsApp bildirimi kapalı, atlandı.";

            if (sent > 0) TempData["Success"] = summary;
            else TempData["Error"] = summary;

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Otomatik ödeme hatırlatıcısının kaç gün önceden (ör. "7,1" → vadeye 7 gün ve 1 gün kala)
        /// tetikleneceğini ve gönderilecek mesaj şablonunu kaydeder. PaymentReminderBackgroundService
        /// ve PaymentNotificationService.NotifyReminderAsync bu değerleri bir sonraki çalışmada
        /// (en geç 24 saat içinde, uygulama yeniden başlatılırsa hemen) okur.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveReminderSettings(string checkpoints, string? messageTemplate)
        {
            var days = (checkpoints ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var n) ? n : (int?)null)
                .Where(n => n.HasValue && n.Value > 0)
                .Select(n => n!.Value)
                .Distinct()
                .OrderByDescending(n => n)
                .ToList();

            if (days.Count == 0)
            {
                TempData["Error"] = "Geçerli en az bir gün sayısı seçin (örn: 7 gün, 1 gün).";
                return RedirectToAction(nameof(Index));
            }

            var template = string.IsNullOrWhiteSpace(messageTemplate)
                ? PaymentNotificationService.DefaultReminderMessageTemplate
                : messageTemplate.Trim();

            await _settings.SaveAllAsync(new Dictionary<string, string>
            {
                [ReminderCheckpointsKey] = string.Join(",", days),
                [ReminderMessageTemplateKey] = template
            });

            TempData["Success"] = $"Otomatik hatırlatıcı ayarları güncellendi: vadeye {string.Join(", ", days)} gün kala hatırlatma gönderilecek.";
            return RedirectToAction(nameof(Index));
        }
    }
}
