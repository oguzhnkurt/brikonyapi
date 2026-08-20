using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.KatMaliki.Controllers
{
    /// <summary>Kat Maliki Portalı "Sohbet" ekranı: proje bazlı malikler arası mesajlaşma.</summary>
    [Area("KatMaliki"), Authorize(Roles = "KatMaliki")]
    public class ChatController : Controller
    {
        private const int PageSize = 50;

        private readonly AppDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public ChatController(AppDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index(int? projectId)
        {
            var userId = _users.GetUserId(User);
            if (userId == null) return Challenge();

            var owner = await _db.Owners.FirstOrDefaultAsync(o => o.UserId == userId);

            if (owner == null) return NotFound("Bu kullanıcıya bağlı bir kat maliki profili bulunamadı.");

            // GÜVENLİK: Malikin yalnızca admin'in sohbet erişimi verdiği projelere erişimi vardır
            // (bağımsız bölüm sahipliğinden bağımsız — Admin > Kat Malikleri > Erişim'den atanır).
            var projects = await _db.OwnerProjectAccesses
                .Where(a => a.OwnerId == owner.Id && a.CanChat)
                .Include(a => a.Project)
                .Select(a => a.Project!)
                .ToListAsync();

            ViewBag.Projects = projects;
            ViewBag.Owner = owner;
            ViewBag.CurrentUserId = userId;
            ViewBag.OwnerId = owner.Id;

            if (!projects.Any())
            {
                ViewBag.SelectedProject = null;
                return View(new List<ChatMessage>());
            }

            // İstenen proje malikin projeleri arasında değilse ilk projeye düşülür.
            var selected = projects.FirstOrDefault(p => p.Id == projectId) ?? projects.First();
            ViewBag.SelectedProject = selected;

            var messages = await _db.ChatMessages
                .Include(m => m.ChatPoll!).ThenInclude(p => p.Options)
                .Include(m => m.ChatPoll!).ThenInclude(p => p.Votes)
                .Where(m => m.ProjectId == selected.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Take(PageSize)
                .ToListAsync();

            messages.Reverse(); // en eski üstte
            return View(messages);
        }

        /// <summary>Kendi mesajını silme (yalnızca gönderen kendi mesajını silebilir).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOwn(int id)
        {
            var userId = _users.GetUserId(User);
            var message = await _db.ChatMessages.FirstOrDefaultAsync(m => m.Id == id);

            if (message == null || message.SenderUserId != userId)
                return NotFound();

            message.IsDeleted = true;
            message.DeletedAt = DateTime.Now;
            message.DeletedByUserId = userId;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Mesajınız silindi.";
            return RedirectToAction(nameof(Index), new { projectId = message.ProjectId });
        }
    }
}
