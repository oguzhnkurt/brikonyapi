using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize]
    public class ReferencesController : Controller
    {
        private readonly ReferenceService _refs;
        private readonly IWebHostEnvironment _env;

        public ReferencesController(ReferenceService refs, IWebHostEnvironment env)
        {
            _refs = refs;
            _env  = env;
        }

        public async Task<IActionResult> Index() => View(await _refs.GetAllAsync());

        public async Task<IActionResult> Create()
        {
            var all = await _refs.GetAllAsync();
            var next = all.Any() ? all.Max(r => r.OrderIndex) + 1 : 1;
            return View(new Reference { OrderIndex = next });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reference reference, IFormFile? logo)
        {
            ModelState.Remove("LogoPath");
            if (!ModelState.IsValid) return View(reference);

            if (logo?.Length > 0)
                reference.LogoPath = await SaveLogoAsync(logo);

            await _refs.CreateAsync(reference);
            TempData["Success"] = "Referans eklendi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var r = await _refs.GetByIdAsync(id);
            if (r == null) return NotFound();
            return View(r);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Reference reference, IFormFile? logo)
        {
            ModelState.Remove("LogoPath");
            if (!ModelState.IsValid) return View(reference);

            var existing = await _refs.GetByIdAsync(reference.Id);
            if (existing == null) return NotFound();

            existing.Name       = reference.Name;
            existing.OrderIndex = reference.OrderIndex;
            existing.IsActive   = reference.IsActive;

            if (logo?.Length > 0)
            {
                DeleteLogo(existing.LogoPath);
                existing.LogoPath = await SaveLogoAsync(logo);
            }

            await _refs.UpdateAsync(existing);
            TempData["Success"] = "Referans güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var r = await _refs.GetByIdAsync(id);
            if (r != null) DeleteLogo(r.LogoPath);
            await _refs.DeleteAsync(id);
            TempData["Success"] = "Referans silindi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveLogoAsync(IFormFile file)
        {
            var dir = Path.Combine(_env.WebRootPath, "images/refs");
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid()}.png";
            var fullPath = Path.Combine(dir, fileName);

            using var stream = file.OpenReadStream();
            using var image  = await Image.LoadAsync(stream);
            if (image.Width > 400) image.Mutate(x => x.Resize(400, 0));
            await image.SaveAsPngAsync(fullPath);

            return $"/images/refs/{fileName}";
        }

        private void DeleteLogo(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var full = Path.Combine(_env.WebRootPath, path.TrimStart('/'));
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }
    }
}
