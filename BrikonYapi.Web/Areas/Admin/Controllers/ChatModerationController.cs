using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>Malik sohbetlerinin izlenmesi ve moderasyonu.</summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class ChatModerationController : Controller
    {
        private const int PageSize = 100;

        private readonly AppDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public ChatModerationController(AppDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index(int? projectId, bool showDeleted = false)
        {
            // Sohbeti olan veya bağımsız bölümü olan projeler
            var projectIds = await _db.Units.Select(u => u.ProjectId).Distinct().ToListAsync();
            var projects = await _db.Projects
                .Where(p => projectIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Projects = projects;
            ViewBag.ShowDeleted = showDeleted;

            var selected = projects.FirstOrDefault(p => p.Id == projectId) ?? projects.FirstOrDefault();
            ViewBag.SelectedProject = selected;

            if (selected == null) return View(new List<ChatMessage>());

            var query = _db.ChatMessages
                .Include(m => m.ChatPoll!).ThenInclude(p => p.Options)
                .Include(m => m.ChatPoll!).ThenInclude(p => p.Votes)
                .Where(m => m.ProjectId == selected.Id);
            if (!showDeleted) query = query.Where(m => !m.IsDeleted);

            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Take(PageSize)
                .ToListAsync();

            return View(messages);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var message = await _db.ChatMessages.FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();

            // Sert silme yapılmaz; kayıt moderasyon izi olarak saklanır.
            message.IsDeleted = true;
            message.DeletedAt = DateTime.Now;
            message.DeletedByUserId = _users.GetUserId(User);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Mesaj sohbetten kaldırıldı.";
            return RedirectToAction(nameof(Index), new { projectId = message.ProjectId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var message = await _db.ChatMessages.FirstOrDefaultAsync(m => m.Id == id);
            if (message == null) return NotFound();

            message.IsDeleted = false;
            message.DeletedAt = null;
            message.DeletedByUserId = null;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Mesaj geri alındı.";
            return RedirectToAction(nameof(Index), new { projectId = message.ProjectId, showDeleted = true });
        }
    }
}
