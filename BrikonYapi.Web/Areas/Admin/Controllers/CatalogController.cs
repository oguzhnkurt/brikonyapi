using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class CatalogController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly CatalogGeneratorService _generator;

        public CatalogController(AppDbContext db, IWebHostEnvironment env, CatalogGeneratorService generator)
        {
            _db = db;
            _env = env;
            _generator = generator;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _db.Catalogs.OrderByDescending(c => c.CreatedAt).ToListAsync();
            return View(list);
        }

        // Manuel PDF yükleme
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadManual(string title, string? description, IFormFile pdf)
        {
            if (pdf == null || pdf.Length == 0 || !pdf.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Lütfen geçerli bir PDF dosyası yükleyin.";
                return RedirectToAction(nameof(Index));
            }

            var dir = Path.Combine(_env.WebRootPath, "uploads/catalogs");
            Directory.CreateDirectory(dir);
            var fileName = $"katalog-manuel-{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var fullPath = Path.Combine(dir, fileName);

            var tempPath = fullPath + ".tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create))
                await pdf.CopyToAsync(stream);

            // Ghostscript ile sıkıştır (web için optimize)
            var compressed = await CompressPdfAsync(tempPath, fullPath);
            if (!compressed) System.IO.File.Move(tempPath, fullPath, overwrite: true);
            else if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);

            var catalog = new Catalog
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Manuel Katalog" : title,
                Description = description,
                Type = CatalogType.Manual,
                PdfPath = $"/uploads/catalogs/{fileName}",
                IsActive = false,
                CreatedAt = DateTime.Now
            };
            _db.Catalogs.Add(catalog);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Manuel katalog yüklendi.";
            return RedirectToAction(nameof(Index));
        }

        // Otomatik katalog oluştur (projelerden PDF üret)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAuto(string? title, string? description)
        {
            try
            {
                var pdfPath = await _generator.GenerateAsync();

                var catalog = new Catalog
                {
                    Title = string.IsNullOrWhiteSpace(title) ? $"Otomatik Katalog — {DateTime.Now:dd.MM.yyyy}" : title,
                    Description = description ?? "Projelerden otomatik oluşturuldu.",
                    Type = CatalogType.Auto,
                    PdfPath = pdfPath,
                    IsActive = false,
                    CreatedAt = DateTime.Now
                };
                _db.Catalogs.Add(catalog);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Katalog başarıyla oluşturuldu!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Katalog oluşturulurken hata: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // Aktif katalog seç
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(int id)
        {
            var all = await _db.Catalogs.ToListAsync();
            foreach (var c in all) c.IsActive = (c.Id == id);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Aktif katalog güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var catalog = await _db.Catalogs.FindAsync(id);
            if (catalog != null)
            {
                if (catalog.Type == CatalogType.Manual && !string.IsNullOrEmpty(catalog.PdfPath))
                {
                    var full = Path.Combine(_env.WebRootPath, catalog.PdfPath.TrimStart('/'));
                    if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                }
                _db.Catalogs.Remove(catalog);
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Katalog silindi.";
            return RedirectToAction(nameof(Index));
        }

        private static async Task<bool> CompressPdfAsync(string inputPath, string outputPath)
        {
            try
            {
                var gs = OperatingSystem.IsWindows() ? "gswin64c" : "gs";
                var psi = new ProcessStartInfo(gs,
                    $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.5 -dPDFSETTINGS=/ebook " +
                    $"-dNOPAUSE -dQUIET -dBATCH -dFastWebView=true " +
                    $"-sOutputFile=\"{outputPath}\" \"{inputPath}\"")
                {
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                await proc.WaitForExitAsync();
                // Sıkıştırılmış dosya orijinalden büyükse orijinali kullan
                if (System.IO.File.Exists(outputPath) &&
                    new FileInfo(outputPath).Length >= new FileInfo(inputPath).Length)
                {
                    System.IO.File.Delete(outputPath);
                    return false;
                }
                return proc.ExitCode == 0 && System.IO.File.Exists(outputPath);
            }
            catch
            {
                return false;
            }
        }
    }
}
