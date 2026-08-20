using BrikonYapi.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.KatMaliki.Controllers
{
    /// <summary>Kat Maliki Portalı "Sıkça Sorulan Sorular" sayfası.</summary>
    [Area("KatMaliki"), Authorize(Roles = "KatMaliki")]
    public class FaqController : Controller
    {
        private readonly AppDbContext _db;

        public FaqController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _db.Faqs
                .Where(f => f.IsActive)
                .OrderBy(f => f.OrderIndex)
                .ThenBy(f => f.CreatedAt)
                .ToListAsync();

            return View(list);
        }
    }
}
