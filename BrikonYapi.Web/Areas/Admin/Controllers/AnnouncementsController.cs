using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>Kat Maliki Portalı duyuru / haberlerinin yönetimi.</summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class AnnouncementsController : Controller
    {
        private static readonly string[] AllowedImageExt = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageBytes = 10 * 1024 * 1024;

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public AnnouncementsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _db.Announcements.OrderByDescending(a => a.CreatedAt).ToListAsync();
            return View(list);
        }

        public IActionResult Create() => View(new Announcement());

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> Create(Announcement announcement, IFormFile? cover)
        {
            if (!ModelState.IsValid) return View(announcement);

            var (path, error) = await SaveCoverAsync(cover);
            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
                return View(announcement);
            }

            announcement.CoverImagePath = path;
            announcement.CreatedAt = DateTime.Now;
            _db.Announcements.Add(announcement);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Duyuru eklendi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await _db.Announcements.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, Announcement announcement, IFormFile? cover, bool removeCover = false)
        {
            var existing = await _db.Announcements.FindAsync(id);
            if (existing == null) return NotFound();

            var (path, error) = await SaveCoverAsync(cover);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Edit), new { id });
            }

            existing.Title = announcement.Title;
            existing.Body = announcement.Body;
            existing.Tag = announcement.Tag;
            existing.Summary = announcement.Summary;
            existing.Source = announcement.Source;
            existing.ShowOnHome = announcement.ShowOnHome;
            existing.IsActive = announcement.IsActive;
            existing.OrderIndex = announcement.OrderIndex;
            existing.UpdatedAt = DateTime.Now;

            if (path != null)
            {
                DeleteCoverFile(existing.CoverImagePath);
                existing.CoverImagePath = path;
            }
            else if (removeCover)
            {
                DeleteCoverFile(existing.CoverImagePath);
                existing.CoverImagePath = null;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Duyuru güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.Announcements.FindAsync(id);
            if (item != null)
            {
                DeleteCoverFile(item.CoverImagePath);
                _db.Announcements.Remove(item);
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Duyuru silindi.";
            return RedirectToAction(nameof(Index));
        }

        // ── Yardımcılar ──────────────────────────────────────────

        /// <summary>Kapak görselini doğrular ve diske yazar. Hata varsa (null, mesaj) döner.</summary>
        private async Task<(string? Path, string? Error)> SaveCoverAsync(IFormFile? cover)
        {
            if (cover == null || cover.Length == 0) return (null, null);

            if (cover.Length > MaxImageBytes)
                return (null, "Kapak görseli 10 MB'ı aşamaz.");

            var ext = Path.GetExtension(cover.FileName).ToLowerInvariant();
            if (!AllowedImageExt.Contains(ext))
                return (null, "Kapak görseli için sadece JPG, PNG veya WEBP yükleyebilirsiniz.");

            var dir = Path.Combine(_env.WebRootPath, "uploads", "news");
            Directory.CreateDirectory(dir);

            // Dosya adı kullanıcıdan alınmaz (path traversal önlemi).
            var fileName = $"{Guid.NewGuid()}{ext}";
            await using (var fs = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
                await cover.CopyToAsync(fs);

            return ($"/uploads/news/{fileName}", null);
        }

        /// <summary>Yalnızca kendi yükleme klasörümüzdeki dosyayı siler.</summary>
        private void DeleteCoverFile(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return;
            try
            {
                var rel = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(_env.WebRootPath, rel));
                var root = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "news"));
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* dosya silinemezse kayıt yine de güncellenir */ }
        }
    }
}
