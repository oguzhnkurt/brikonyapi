using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class CertificatesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public CertificatesController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _db.Certificates.OrderBy(c => c.OrderIndex).ToListAsync();
            return View(list);
        }

        public IActionResult Create() => View(new Certificate());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Certificate cert, IFormFile? image)
        {
            ModelState.Remove("ImagePath");
            if (!ModelState.IsValid) return View(cert);

            if (image?.Length > 0)
                cert.ImagePath = await SaveImageAsync(image);

            var max = await _db.Certificates.AnyAsync() ? await _db.Certificates.MaxAsync(c => c.OrderIndex) : 0;
            cert.OrderIndex = max + 1;
            cert.CreatedAt = DateTime.Now;

            _db.Certificates.Add(cert);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Sertifika eklendi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var cert = await _db.Certificates.FindAsync(id);
            if (cert == null) return NotFound();
            return View(cert);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Certificate cert, IFormFile? image)
        {
            ModelState.Remove("ImagePath");
            if (!ModelState.IsValid) return View(cert);

            var existing = await _db.Certificates.FindAsync(cert.Id);
            if (existing == null) return NotFound();

            existing.Title = cert.Title;
            existing.Description = cert.Description;
            existing.OrderIndex = cert.OrderIndex;
            existing.IsActive = cert.IsActive;

            if (image?.Length > 0)
            {
                DeleteImage(existing.ImagePath);
                existing.ImagePath = await SaveImageAsync(image);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Sertifika güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cert = await _db.Certificates.FindAsync(id);
            if (cert != null)
            {
                DeleteImage(cert.ImagePath);
                _db.Certificates.Remove(cert);
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Sertifika silindi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads/certificates");
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid()}.jpg";
            var fullPath = Path.Combine(dir, fileName);

            using var stream = file.OpenReadStream();
            using var img = await Image.LoadAsync(stream);
            if (img.Width > 1200) img.Mutate(x => x.Resize(1200, 0));
            await img.SaveAsJpegAsync(fullPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 90 });

            return $"/uploads/certificates/{fileName}";
        }

        private void DeleteImage(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var full = Path.Combine(_env.WebRootPath, path.TrimStart('/'));
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }
    }
}
