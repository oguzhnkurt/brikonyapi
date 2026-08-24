using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>Kat Maliki Portalı "İlerleme" ekranının yönetimi: genel ilerleme, teslim tarihi,
    /// 360° tur bağlantısı, inşaat aşamaları ve saha fotoğrafları.</summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class ProjectProgressController : Controller
    {
        private static readonly string[] AllowedImageExt = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageBytes = 10 * 1024 * 1024;

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly PaymentNotificationService _notify;

        public ProjectProgressController(AppDbContext db, IWebHostEnvironment env, PaymentNotificationService notify)
        {
            _db = db;
            _env = env;
            _notify = notify;
        }

        /// <summary>Bağımsız bölümü olan (yani portalda görünen) projeleri listeler.</summary>
        public async Task<IActionResult> Index()
        {
            var projects = await _db.Projects
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.IsFeatured)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var unitCounts = await _db.Units
                .GroupBy(u => u.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

            var stageCounts = await _db.ProjectStages
                .GroupBy(s => s.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

            var photoCounts = await _db.SitePhotos
                .GroupBy(s => s.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

            ViewBag.UnitCounts = unitCounts;
            ViewBag.StageCounts = stageCounts;
            ViewBag.PhotoCounts = photoCounts;

            return View(projects);
        }

        public async Task<IActionResult> Manage(int id)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();

            ViewBag.Stages = await _db.ProjectStages
                .Where(s => s.ProjectId == id)
                .OrderBy(s => s.OrderIndex)
                .ToListAsync();

            ViewBag.Photos = await _db.SitePhotos
                .Where(s => s.ProjectId == id)
                .OrderByDescending(s => s.TakenAt)
                .ToListAsync();

            return View(project);
        }

        // ── Genel bilgiler ───────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGeneral(int id, int overallProgress, DateTime? estimatedDelivery, string? virtualTourUrl)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();

            // GÜVENLİK: tur bağlantısı portalda iframe'e gömüldüğü için yalnızca mutlak HTTPS adresine izin verilir.
            var url = (virtualTourUrl ?? "").Trim();
            if (url.Length > 0)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                {
                    TempData["Error"] = "Sanal tur bağlantısı https:// ile başlayan tam bir adres olmalıdır.";
                    return RedirectToAction(nameof(Manage), new { id });
                }
                url = uri.AbsoluteUri;
            }

            project.OverallProgressPercentage = Math.Clamp(overallProgress, 0, 100);
            project.EstimatedDeliveryDate = estimatedDelivery;
            project.VirtualTourUrl = url.Length > 0 ? url : null;
            project.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Proje ilerleme bilgileri kaydedildi.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        // ── Aşamalar ─────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStage(
            int id, string name, int orderIndex,
            int weightPercentage = 0, DateTime? plannedStartDate = null, DateTime? plannedEndDate = null,
            decimal? estimatedBudget = null, int progressPercentage = 0)
        {
            if (!await _db.Projects.AnyAsync(p => p.Id == id)) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Aşama adı zorunludur.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            var clampedProgress = Math.Clamp(progressPercentage, 0, 100);

            _db.ProjectStages.Add(new ProjectStage
            {
                ProjectId = id,
                Name = name.Trim(),
                OrderIndex = orderIndex,
                WeightPercentage = Math.Clamp(weightPercentage, 0, 100),
                PlannedStartDate = plannedStartDate,
                PlannedEndDate = plannedEndDate,
                EstimatedBudget = estimatedBudget,
                ProgressPercentage = clampedProgress,
                Status = clampedProgress >= 100 ? ProjectStageStatus.Completed : ProjectStageStatus.Pending,
                CompletedAt = clampedProgress >= 100 ? DateTime.Now : null,
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "İş adımı eklendi.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        /// <summary>Bir iş adımının tüm alanlarını (SantiyePro tarzı "İş Adımı Düzenle" formundan) günceller.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStageDetails(
            int stageId, string name, int orderIndex, int weightPercentage,
            DateTime? plannedStartDate, DateTime? plannedEndDate, decimal? estimatedBudget, int progressPercentage)
        {
            var stage = await _db.ProjectStages.FirstOrDefaultAsync(s => s.Id == stageId);
            if (stage == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Aşama adı zorunludur.";
                return RedirectToAction(nameof(Manage), new { id = stage.ProjectId });
            }

            stage.Name = name.Trim();
            stage.OrderIndex = orderIndex;
            stage.WeightPercentage = Math.Clamp(weightPercentage, 0, 100);
            stage.PlannedStartDate = plannedStartDate;
            stage.PlannedEndDate = plannedEndDate;
            stage.EstimatedBudget = estimatedBudget;
            stage.ProgressPercentage = Math.Clamp(progressPercentage, 0, 100);
            stage.UpdatedAt = DateTime.Now;

            // İlerleme %100 olduysa ve durum hâlâ tamamlanmadıysa otomatik "Tamamlandı" işaretle.
            if (stage.ProgressPercentage >= 100 && stage.Status != ProjectStageStatus.Completed)
            {
                stage.Status = ProjectStageStatus.Completed;
                stage.CompletedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "İş adımı güncellendi.";
            return RedirectToAction(nameof(Manage), new { id = stage.ProjectId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStageStatus(int stageId, ProjectStageStatus status)
        {
            var stage = await _db.ProjectStages.FirstOrDefaultAsync(s => s.Id == stageId);
            if (stage == null) return NotFound();

            var wasCompleted = stage.Status == ProjectStageStatus.Completed;

            stage.Status = status;
            stage.CompletedAt = status == ProjectStageStatus.Completed ? DateTime.Now : null;
            stage.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            // Aşama yeni tamamlandıysa (daha önce tamamlanmamışsa), bu aşamaya bağlı ve henüz
            // ödenmemiş hakediş taksitleri olan malikleri bilgilendir.
            if (status == ProjectStageStatus.Completed && !wasCompleted)
            {
                var linkedSchedules = await _db.PaymentSchedules
                    .Include(p => p.Unit).ThenInclude(u => u!.Owner)
                    .Where(p => p.ProjectStageId == stageId && p.Status == PaymentScheduleStatus.Pending)
                    .ToListAsync();

                foreach (var schedule in linkedSchedules)
                {
                    var owner = schedule.Unit?.Owner;
                    if (owner != null)
                        await _notify.NotifyStageReachedAsync(owner, schedule, stage);
                }
            }

            TempData["Success"] = "Aşama durumu güncellendi.";
            return RedirectToAction(nameof(Manage), new { id = stage.ProjectId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStage(int stageId)
        {
            var stage = await _db.ProjectStages.FirstOrDefaultAsync(s => s.Id == stageId);
            if (stage == null) return NotFound();

            var projectId = stage.ProjectId;

            // Bu aşamaya bağlı taksitlerin bağlantısını temizle (FK NO ACTION ile SQL Server'da
            // "cascade paths" hatası olmasın diye taksitler değil, yalnızca bağlantı kaldırılıyor).
            var linked = await _db.PaymentSchedules.Where(p => p.ProjectStageId == stageId).ToListAsync();
            foreach (var p in linked) p.ProjectStageId = null;

            _db.ProjectStages.Remove(stage);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Aşama silindi.";
            return RedirectToAction(nameof(Manage), new { id = projectId });
        }

        /// <summary>Projeye standart kentsel dönüşüm aşamalarını tek tuşla ekler.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SeedDefaultStages(int id)
        {
            if (!await _db.Projects.AnyAsync(p => p.Id == id)) return NotFound();

            if (await _db.ProjectStages.AnyAsync(s => s.ProjectId == id))
            {
                TempData["Error"] = "Bu projede zaten aşama tanımlı. Önce mevcut aşamaları silin.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            var defaults = new[]
            {
                "Planlama", "Kazı", "Temel", "Karkas",
                "Dış Cephe", "İç Mekan", "Bitirme", "Teslim"
            };

            var i = 1;
            foreach (var name in defaults)
            {
                _db.ProjectStages.Add(new ProjectStage
                {
                    ProjectId = id,
                    Name = name,
                    OrderIndex = i++,
                    Status = ProjectStageStatus.Pending,
                    CreatedAt = DateTime.Now
                });
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Standart inşaat aşamaları eklendi. Durumlarını buradan güncelleyebilirsiniz.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        /// <summary>Yandan açılan "Standart Aşama Listesini Düzenle" panelinde hazırlanan iş adımı listesini
        /// SADECE bu projeye ekler (çoklu / özelleştirilebilir "Standart aşamaları ekle"). Bu projede aynı
        /// isimli (büyük/küçük harf duyarsız) bir adım zaten varsa o satır atlanır, panel tekrar tekrar
        /// kullanılabilir.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAddStages(int id, string stepsText)
        {
            if (!await _db.Projects.AnyAsync(p => p.Id == id)) return NotFound();

            var lines = (stepsText ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!lines.Any())
            {
                TempData["Error"] = "En az bir iş adımı girin.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            var existingStages = await _db.ProjectStages.Where(s => s.ProjectId == id).ToListAsync();
            var existingNames = existingStages.Select(s => s.Name.Trim().ToLowerInvariant()).ToHashSet();
            var nextOrder = existingStages.Any() ? existingStages.Max(s => s.OrderIndex) + 1 : 1;
            var added = 0;

            foreach (var line in lines)
            {
                if (existingNames.Contains(line.ToLowerInvariant())) continue;

                _db.ProjectStages.Add(new ProjectStage
                {
                    ProjectId = id,
                    Name = line,
                    OrderIndex = nextOrder++,
                    Status = ProjectStageStatus.Pending,
                    CreatedAt = DateTime.Now
                });
                existingNames.Add(line.ToLowerInvariant());
                added++;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = added > 0
                ? $"{added} iş adımı eklendi."
                : "Girilen iş adımlarının tamamı zaten mevcut, yeni kayıt eklenmedi.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        // ── Saha fotoğrafları ────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> AddPhoto(int id, IFormFile photo, string? caption, DateTime? takenAt, bool is360)
        {
            if (!await _db.Projects.AnyAsync(p => p.Id == id)) return NotFound();

            if (photo == null || photo.Length == 0)
            {
                TempData["Error"] = "Lütfen bir fotoğraf seçin.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            if (photo.Length > MaxImageBytes)
            {
                TempData["Error"] = "Fotoğraf 10 MB'ı aşamaz.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedImageExt.Contains(ext))
            {
                TempData["Error"] = "Sadece JPG, PNG veya WEBP yükleyebilirsiniz.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            var dir = Path.Combine(_env.WebRootPath, "uploads", "site-photos");
            Directory.CreateDirectory(dir);

            // Dosya adı kullanıcıdan gelmez; path traversal riski yok.
            var fileName = $"{Guid.NewGuid()}{ext}";
            await using (var fs = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
                await photo.CopyToAsync(fs);

            _db.SitePhotos.Add(new SitePhoto
            {
                ProjectId = id,
                ImagePath = $"/uploads/site-photos/{fileName}",
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
                TakenAt = takenAt ?? DateTime.Now,
                Is360 = is360,
                IsActive = true,
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Saha fotoğrafı eklendi.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhoto(int photoId)
        {
            var photo = await _db.SitePhotos.FirstOrDefaultAsync(p => p.Id == photoId);
            if (photo == null) return NotFound();

            var projectId = photo.ProjectId;

            // Diskteki dosyayı da temizle (yalnızca kendi yükleme klasörümüzden).
            try
            {
                var rel = photo.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(_env.WebRootPath, rel));
                var uploadsRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "site-photos"));
                if (full.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* dosya silinemezse kayıt yine de kaldırılır */ }

            _db.SitePhotos.Remove(photo);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Saha fotoğrafı silindi.";
            return RedirectToAction(nameof(Manage), new { id = projectId });
        }
    }
}
