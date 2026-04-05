using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize]
    public class ProjectsController : Controller
    {
        private readonly ProjectService _projects;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;

        public ProjectsController(ProjectService projects, IWebHostEnvironment env, IConfiguration config, AppDbContext db)
        {
            _projects = projects;
            _env      = env;
            _config   = config;
            _db       = db;
        }

        private async Task LoadCategoriesAsync(int? selectedId = null)
        {
            var cats = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.OrderIndex).ThenBy(c => c.Name).ToListAsync();
            ViewBag.Categories = new SelectList(cats, "Id", "Name", selectedId);
        }

        public async Task<IActionResult> Index() => View(await _projects.GetAllAsync());

        public async Task<IActionResult> Create()
        {
            await LoadCategoriesAsync();
            var all  = await _projects.GetAllAsync();
            var next = all.Any() ? all.Max(p => p.OrderIndex) + 1 : 1;
            return View(new Project { OrderIndex = next });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project, IFormFile? mainImage, IFormFile? projectVideo, IFormFile[]? galleryImages, IFormFile[]? planImages)
        {
            ModelState.Remove("Slug"); ModelState.Remove("Images"); ModelState.Remove("HeroSlides"); ModelState.Remove("Category");
            if (!ModelState.IsValid) { await LoadCategoriesAsync(project.CategoryId); return View(project); }

            project.Slug = _projects.GenerateSlug(project.Name);
            if (mainImage?.Length > 0) project.MainImagePath = await SaveFileAsync(mainImage, "images/projects");
            if (projectVideo?.Length > 0) project.VideoPath = await SaveFileAsync(projectVideo, "videos/projects");

            await _projects.CreateAsync(project);

            if (galleryImages != null)
                foreach (var f in galleryImages.Where(f => f.Length > 0))
                    await _projects.AddImageAsync(new ProjectImage { ProjectId = project.Id, ImagePath = await SaveFileAsync(f, "images/projects"), IsPlan = false });

            if (planImages != null)
                foreach (var f in planImages.Where(f => f.Length > 0))
                    await _projects.AddImageAsync(new ProjectImage { ProjectId = project.Id, ImagePath = await SaveFileAsync(f, "images/projects/plans"), IsPlan = true, Caption = Path.GetFileNameWithoutExtension(f.FileName) });

            TempData["Success"] = "Proje oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var p = await _projects.GetByIdAsync(id);
            if (p == null) return NotFound();
            await LoadCategoriesAsync(p.CategoryId);
            return View(p);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Project project, IFormFile? mainImage, IFormFile? projectVideo, IFormFile[]? galleryImages, IFormFile[]? planImages)
        {
            ModelState.Remove("Slug"); ModelState.Remove("Images"); ModelState.Remove("HeroSlides");

            var existing = await _projects.GetByIdAsync(project.Id);
            if (existing == null) return NotFound();

            if (!ModelState.IsValid)
            {
                project.Images = existing.Images;
                return View(project);
            }

            existing.Name             = project.Name;
            existing.ShortDescription = project.ShortDescription;
            existing.Description      = project.Description;
            existing.Location         = project.Location;
            existing.District         = project.District;
            existing.City             = project.City;
            existing.Status           = project.Status;
            existing.CardTag              = project.CardTag;
            existing.CardVideoAutoplay    = project.CardVideoAutoplay;
            existing.TotalArea        = project.TotalArea;
            existing.UnitCount        = project.UnitCount;
            existing.FloorCount       = project.FloorCount;
            existing.BlockCount       = project.BlockCount;
            existing.StartDate        = project.StartDate;
            existing.EndDate          = project.EndDate;
            existing.Latitude         = project.Latitude;
            existing.Longitude        = project.Longitude;
            existing.IsActive         = project.IsActive;
            existing.IsFeatured       = project.IsFeatured;
            existing.IsMarquee        = project.IsMarquee;
            existing.OrderIndex       = project.OrderIndex;
            existing.Slug             = _projects.GenerateSlug(project.Name);

            if (mainImage?.Length > 0)
            {
                if (!string.IsNullOrEmpty(existing.MainImagePath)) DeleteFile(existing.MainImagePath);
                existing.MainImagePath = await SaveFileAsync(mainImage, "images/projects");
            }

            if (projectVideo?.Length > 0)
            {
                if (!string.IsNullOrEmpty(existing.VideoPath)) DeleteFile(existing.VideoPath);
                existing.VideoPath = await SaveFileAsync(projectVideo, "videos/projects");
            }

            if (galleryImages != null)
                foreach (var f in galleryImages.Where(f => f.Length > 0))
                    await _projects.AddImageAsync(new ProjectImage { ProjectId = existing.Id, ImagePath = await SaveFileAsync(f, "images/projects"), IsPlan = false });

            if (planImages != null)
                foreach (var f in planImages.Where(f => f.Length > 0))
                    await _projects.AddImageAsync(new ProjectImage { ProjectId = existing.Id, ImagePath = await SaveFileAsync(f, "images/projects/plans"), IsPlan = true, Caption = Path.GetFileNameWithoutExtension(f.FileName) });

            await _projects.UpdateAsync(existing);
            TempData["Success"] = "Proje güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Images(int id)
        {
            var p = await _projects.GetByIdAsync(id);
            if (p == null) return NotFound();
            return View(p);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImages(int projectId, IFormFile[]? galleryImages, IFormFile[]? planImages)
        {
            if (galleryImages != null)
                foreach (var f in galleryImages.Where(f => f.Length > 0))
                    await _projects.AddImageAsync(new ProjectImage { ProjectId = projectId, ImagePath = await SaveFileAsync(f, "images/projects"), IsPlan = false });

            if (planImages != null)
                foreach (var f in planImages.Where(f => f.Length > 0))
                    await _projects.AddImageAsync(new ProjectImage { ProjectId = projectId, ImagePath = await SaveFileAsync(f, "images/projects/plans"), IsPlan = true, Caption = Path.GetFileNameWithoutExtension(f.FileName) });

            TempData["Success"] = "Görseller yüklendi.";
            return RedirectToAction(nameof(Images), new { id = projectId });
        }

        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> SaveOrder([FromBody] List<int> ids)
        {
            if (ids == null || ids.Count == 0) return BadRequest();
            for (int i = 0; i < ids.Count; i++)
            {
                var id    = ids[i];
                var order = i + 1;
                await _db.Projects
                    .Where(p => p.Id == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.OrderIndex, order)
                        .SetProperty(p => p.UpdatedAt, DateTime.Now));
            }
            return Ok();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorder()
        {
            var all = await _projects.GetAllAsync(); // ongoing first, then by date
            for (int i = 0; i < all.Count; i++)
            {
                all[i].OrderIndex = i + 1;
                all[i].UpdatedAt  = DateTime.Now;
            }
            _db.Projects.UpdateRange(all);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Sıralar 1'den itibaren yeniden düzenlendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _projects.DeleteAsync(id);
            TempData["Success"] = "Proje silindi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId, int projectId)
        {
            await _projects.DeleteImageAsync(imageId);
            return RedirectToAction(nameof(Edit), new { id = projectId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMainImage(int imageId, int projectId)
        {
            var project = await _projects.GetByIdAsync(projectId);
            var image   = project?.Images.FirstOrDefault(i => i.Id == imageId);
            if (project != null && image != null)
            {
                project.MainImagePath = image.ImagePath;
                await _projects.UpdateAsync(project);
                TempData["Success"] = "Ana görsel güncellendi.";
            }
            return RedirectToAction(nameof(Edit), new { id = projectId });
        }

        private static readonly HashSet<string> _imageExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".jfif", ".png", ".webp", ".gif" };

        private static readonly HashSet<string> _convertVideoExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".mov", ".avi", ".wmv", ".mkv" };

        private static readonly HashSet<string> _allowedVideoExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".avi", ".wmv", ".mkv" };

        // Magic byte imzaları — uzantı sahteciliğini önler
        private static bool IsValidImageBytes(Stream stream)
        {
            var header = new byte[12];
            if (stream.Read(header, 0, 12) < 4) return false;
            stream.Position = 0;

            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
            // PNG: 89 50 4E 47
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
            // GIF: 47 49 46
            if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46) return true;
            // WebP: RIFF....WEBP
            if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) return true;

            return false;
        }

        private static bool IsValidVideoBytes(Stream stream)
        {
            var header = new byte[12];
            if (stream.Read(header, 0, 12) < 8) return false;
            stream.Position = 0;

            // MP4/MOV: ftyp box (offset 4-7)
            if (header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) return true;
            // AVI: RIFF....AVI
            if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46) return true;
            // WMV/ASF: 30 26 B2 75
            if (header[0] == 0x30 && header[1] == 0x26 && header[2] == 0xB2 && header[3] == 0x75) return true;
            // MKV: 1A 45 DF A3
            if (header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3) return true;

            return false;
        }

        private async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            // Dosya boyutu limiti
            var maxBytes = (_config.GetValue<long>("Upload:MaxFileSizeMB", 100)) * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new InvalidOperationException($"Dosya boyutu {maxBytes / 1024 / 1024} MB limitini aşıyor.");

            var dir = Path.Combine(_env.WebRootPath, folder);
            Directory.CreateDirectory(dir);
            var ext      = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(dir, fileName);

            if (_imageExtensions.Contains(ext))
            {
                using var inputStream = file.OpenReadStream();
                if (!IsValidImageBytes(inputStream))
                    throw new InvalidOperationException("Geçersiz görsel dosyası.");
                using var image = await Image.LoadAsync(inputStream);
                // Max 1920px genişlik, oranı koruyarak küçült
                if (image.Width > 1920)
                    image.Mutate(x => x.Resize(1920, 0));
                await image.SaveAsJpegAsync(fullPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 92 });
                // Uzantı .jpg'ye normalize et
                var jpgName = Path.ChangeExtension(fileName, ".jpg");
                if (jpgName != fileName)
                {
                    System.IO.File.Move(fullPath, Path.Combine(dir, jpgName));
                    return $"/{folder}/{jpgName}";
                }
                return $"/{folder}/{fileName}";
            }

            // .mov / .avi gibi formatları .mp4'e dönüştür
            if (_convertVideoExtensions.Contains(ext))
            {
                using var vs = file.OpenReadStream();
                if (!IsValidVideoBytes(vs))
                    throw new InvalidOperationException("Geçersiz video dosyası.");

                var tempPath = fullPath + ".tmp" + ext;
                await using (var tmp = new FileStream(tempPath, FileMode.Create))
                    await file.CopyToAsync(tmp);

                var mp4Name = Path.ChangeExtension(fileName, ".mp4");
                var mp4Path = Path.Combine(dir, mp4Name);
                var ffmpeg  = _config["FFmpegPath"] ?? "ffmpeg";
                var psi     = new System.Diagnostics.ProcessStartInfo(ffmpeg,
                    $"-i \"{tempPath}\" -c:v libx264 -preset fast -crf 23 -c:a aac -movflags +faststart \"{mp4Path}\" -y")
                {
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var proc = System.Diagnostics.Process.Start(psi)!;
                await proc.WaitForExitAsync();
                System.IO.File.Delete(tempPath);
                return $"/{folder}/{mp4Name}";
            }

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/{folder}/{fileName}";
        }

        private void DeleteFile(string path)
        {
            var full = Path.Combine(_env.WebRootPath, path.TrimStart('/'));
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }
    }
}
