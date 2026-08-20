using BrikonYapi.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.KatMaliki.Controllers
{
    /// <summary>Kat Maliki Portalı "Haberler &amp; Projeler" ekranı.</summary>
    [Area("KatMaliki"), Authorize(Roles = "KatMaliki")]
    public class NewsController : Controller
    {
        private readonly AppDbContext _db;

        public NewsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var news = await _db.Announcements
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.OrderIndex)
                .ThenByDescending(a => a.CreatedAt)
                .Take(50)
                .ToListAsync();

            var projects = await _db.Projects
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.StartDate)
                .Take(30)
                .ToListAsync();

            ViewBag.Projects = projects;
            return View(news);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var item = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id && a.IsActive);
            if (item == null) return NotFound();
            return View(item);
        }
    }
}
