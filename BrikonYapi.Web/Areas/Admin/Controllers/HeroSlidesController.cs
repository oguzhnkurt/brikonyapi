using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize]
    public class HeroSlidesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public HeroSlidesController(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

        public async Task<IActionResult> Index() =>
            View(await _db.HeroSlides.Include(h => h.Project).OrderBy(h => h.OrderIndex).ToListAsync());

        public async Task<IActionResult> Create()
        {
            ViewBag.Projects = await _db.Projects.Where(p => p.IsActive).ToListAsync();
            return View(new HeroSlide());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HeroSlide slide, IFormFile? videoFile, IFormFile? bgImage)
        {
            ModelState.Remove("Project");
            if (!ModelState.IsValid) { ViewBag.Projects = await _db.Projects.Where(p => p.IsActive).ToListAsync(); return View(slide); }

            if (videoFile?.Length > 0) slide.VideoPath = await SaveAsync(videoFile, "videos");
            if (bgImage?.Length > 0) slide.BackgroundImagePath = await SaveAsync(bgImage, "images/hero");

            _db.HeroSlides.Add(slide);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Slide oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _db.HeroSlides.FindAsync(id);
            if (s != null) { _db.HeroSlides.Remove(s); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Slide silindi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var s = await _db.HeroSlides.FindAsync(id);
            if (s != null) { s.IsActive = !s.IsActive; await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveAsync(IFormFile file, string folder)
        {
            var dir = Path.Combine(_env.WebRootPath, folder);
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName).ToLower()}";
            await using var s = new FileStream(Path.Combine(dir, name), FileMode.Create);
            await file.CopyToAsync(s);
            return $"/{folder}/{name}";
        }
    }
}
