using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize]
    public class SettingsController : Controller
    {
        private readonly SiteSettingService _settings;
        private readonly IWebHostEnvironment _env;
        public SettingsController(SiteSettingService settings, IWebHostEnvironment env)
        {
            _settings = settings;
            _env      = env;
        }

        public async Task<IActionResult> Index() => View(await _settings.GetAllAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Dictionary<string, string> settings)
        {
            await _settings.SaveAllAsync(settings);
            TempData["Success"] = "Ayarlar kaydedildi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAboutMedia(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Lütfen bir dosya seçin.";
                return RedirectToAction(nameof(Index));
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".mov", ".webm" };
            if (!allowed.Contains(ext))
            {
                TempData["Error"] = "Desteklenmeyen dosya türü. (jpg, png, webp, mp4, mov, webm)";
                return RedirectToAction(nameof(Index));
            }

            var dir = Path.Combine(_env.WebRootPath, "uploads", "about");
            Directory.CreateDirectory(dir);

            var fileName = $"about-media{ext}";
            var fullPath = Path.Combine(dir, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            var path = $"/uploads/about/{fileName}";
            await _settings.SaveAllAsync(new Dictionary<string, string> { ["AboutMediaPath"] = path });

            TempData["Success"] = "Görsel/Video kaydedildi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAboutMedia()
        {
            var current = await _settings.GetAsync("AboutMediaPath");
            if (!string.IsNullOrEmpty(current))
            {
                var full = Path.Combine(_env.WebRootPath, current.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
            }
            await _settings.SaveAllAsync(new Dictionary<string, string> { ["AboutMediaPath"] = "" });
            TempData["Success"] = "Medya silindi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
