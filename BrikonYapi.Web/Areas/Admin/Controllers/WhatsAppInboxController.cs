using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>
    /// WhatsApp gelen kutusu: kat maliklerinin işletme numarasına yazdığı mesajları sohbet bazında
    /// listeler ve son gelen mesajdan sonraki 24 saat içinde serbest metinle yanıt verilmesini sağlar.
    /// Mesajlar WhatsAppWebhookController üzerinden 360dialog webhook'u ile gelir.
    /// </summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class WhatsAppInboxController : Controller
    {
        public static readonly TimeSpan ReplyWindow = TimeSpan.FromHours(24);

        private readonly AppDbContext _db;
        private readonly WhatsAppService _whatsApp;

        public WhatsAppInboxController(AppDbContext db, WhatsAppService whatsApp)
        {
            _db = db;
            _whatsApp = whatsApp;
        }

        public class ConversationItem
        {
            public string Phone { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int? OwnerId { get; set; }
            public string LastBody { get; set; } = "";
            public WhatsAppDirection LastDirection { get; set; }
            public DateTime LastAt { get; set; }
            public int UnreadCount { get; set; }
            public bool CanReply { get; set; }
        }

        public async Task<IActionResult> Index(string? phone = null)
        {
            var all = await _db.WhatsAppMessages
                .Include(m => m.Owner)
                .OrderByDescending(m => m.CreatedAt)
                .Take(2000)
                .ToListAsync();

            var now = DateTime.Now;
            var conversations = all
                .GroupBy(m => m.Phone)
                .Select(g =>
                {
                    var last = g.First();
                    var lastInbound = g.FirstOrDefault(x => x.Direction == WhatsAppDirection.Inbound);
                    var owner = g.Select(x => x.Owner).FirstOrDefault(o => o != null);
                    return new ConversationItem
                    {
                        Phone = g.Key,
                        OwnerId = owner?.Id,
                        DisplayName = owner?.FullName
                                      ?? g.Select(x => x.ContactName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                                      ?? FormatPhone(g.Key),
                        LastBody = last.Body,
                        LastDirection = last.Direction,
                        LastAt = last.CreatedAt,
                        UnreadCount = g.Count(x => x.Direction == WhatsAppDirection.Inbound && !x.IsRead),
                        CanReply = lastInbound != null && now - lastInbound.CreatedAt < ReplyWindow
                    };
                })
                .OrderByDescending(c => c.LastAt)
                .ToList();

            ViewBag.Conversations = conversations;
            ViewBag.WhatsAppConfigured = _whatsApp.IsConfigured;

            if (string.IsNullOrEmpty(phone) && conversations.Count > 0)
                phone = conversations[0].Phone;

            ViewBag.SelectedPhone = phone;
            List<WhatsAppMessage> thread = new();
            if (!string.IsNullOrEmpty(phone))
            {
                thread = await _db.WhatsAppMessages
                    .Where(m => m.Phone == phone)
                    .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
                    .ToListAsync();

                // Açılan sohbetin okunmamış mesajlarını okundu işaretle.
                var unread = thread.Where(m => m.Direction == WhatsAppDirection.Inbound && !m.IsRead).ToList();
                if (unread.Count > 0)
                {
                    unread.ForEach(m => m.IsRead = true);
                    await _db.SaveChangesAsync();
                    var conv = conversations.FirstOrDefault(c => c.Phone == phone);
                    if (conv != null) conv.UnreadCount = 0;
                }

                var lastIn = thread.LastOrDefault(m => m.Direction == WhatsAppDirection.Inbound);
                ViewBag.ReplyDeadline = lastIn?.CreatedAt.Add(ReplyWindow);
                ViewBag.Selected = conversations.FirstOrDefault(c => c.Phone == phone);
            }

            return View("~/Areas/Admin/Views/Notifications/WhatsAppInbox.cshtml", thread);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(string phone, string message)
        {
            phone = new string((phone ?? "").Where(char.IsDigit).ToArray());
            message = (message ?? "").Trim();

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(message))
            {
                TempData["Error"] = "Mesaj boş olamaz.";
                return RedirectToAction(nameof(Index), new { phone });
            }
            if (message.Length > 4096)
            {
                TempData["Error"] = "Mesaj en fazla 4096 karakter olabilir.";
                return RedirectToAction(nameof(Index), new { phone });
            }

            var lastIn = await _db.WhatsAppMessages
                .Where(m => m.Phone == phone && m.Direction == WhatsAppDirection.Inbound)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastIn == null || DateTime.Now - lastIn.CreatedAt >= ReplyWindow)
            {
                TempData["Error"] = "24 saatlik yanıt süresi dolmuş. Bu kişiye artık yalnızca Bildirimler ekranındaki onaylı şablonla yazabilirsiniz.";
                return RedirectToAction(nameof(Index), new { phone });
            }

            var (ok, error, messageId) = await _whatsApp.SendTextAsync(phone, message);

            _db.WhatsAppMessages.Add(new WhatsAppMessage
            {
                Phone = phone,
                OwnerId = lastIn.OwnerId,
                ContactName = lastIn.ContactName,
                Direction = WhatsAppDirection.Outbound,
                MessageType = "text",
                Body = message,
                WaMessageId = messageId,
                Status = ok ? "sent" : "failed",
                ErrorMessage = error,
                IsRead = true,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            if (ok) TempData["Success"] = "Yanıt gönderildi.";
            else TempData["Error"] = error ?? "Yanıt gönderilemedi.";

            return RedirectToAction(nameof(Index), new { phone });
        }

        /// <summary>Sol menüdeki rozet için okunmamış gelen mesaj sayısı.</summary>
        [HttpGet]
        public async Task<IActionResult> UnreadCount() =>
            Json(await _db.WhatsAppMessages.CountAsync(m => m.Direction == WhatsAppDirection.Inbound && !m.IsRead));

        public static string FormatPhone(string waId)
        {
            var d = new string((waId ?? "").Where(char.IsDigit).ToArray());
            if (d.Length == 12 && d.StartsWith("90"))
                return $"+90 {d.Substring(2, 3)} {d.Substring(5, 3)} {d.Substring(8, 2)} {d.Substring(10, 2)}";
            return "+" + d;
        }
    }
}
