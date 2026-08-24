using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>Kat Maliki Portalı oylamalarının (anketlerin) yönetimi.</summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class PollsController : Controller
    {
        private static readonly string[] AllowedImageExt = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageBytes = 10 * 1024 * 1024;

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public PollsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var polls = await _db.Polls
                .Include(p => p.Project)
                .Include(p => p.Options)
                .Include(p => p.Votes)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(polls);
        }

        public async Task<IActionResult> Create()
        {
            await FillProjectsAsync();
            return View(new Poll());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Poll poll, string[] optionTexts)
        {
            var texts = (optionTexts ?? Array.Empty<string>())
                .Select(t => (t ?? "").Trim())
                .Where(t => t.Length > 0)
                .ToList();

            if (texts.Count < 2)
            {
                TempData["Error"] = "En az iki seçenek girmelisiniz.";
                await FillProjectsAsync();
                return View(poll);
            }

            // GÜVENLİK/DOĞRULUK: SetSchedule'daki aynı kural burada da uygulanmazsa, oluşturma
            // formundan bitişi başlangıçtan önceki bir tarihe ayarlamak mümkün oluyor ve oylama
            // hiçbir zaman "Aktif" olmayan mantıksal olarak tutarsız bir durumda kalıyordu.
            if (poll.StartsAt.HasValue && poll.EndsAt.HasValue && poll.EndsAt.Value <= poll.StartsAt.Value)
            {
                TempData["Error"] = "Bitiş tarihi başlangıçtan sonra olmalıdır.";
                await FillProjectsAsync();
                return View(poll);
            }

            poll.CreatedAt = DateTime.Now;
            for (var i = 0; i < texts.Count; i++)
                poll.Options.Add(new PollOption { Text = texts[i], OrderIndex = i + 1 });

            _db.Polls.Add(poll);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Oylama oluşturuldu. Seçenek görsellerini düzenleme ekranından ekleyebilirsiniz.";
            return RedirectToAction(nameof(Manage), new { id = poll.Id });
        }

        public async Task<IActionResult> Manage(int id)
        {
            var poll = await _db.Polls
                .Include(p => p.Project)
                .Include(p => p.Options)
                .Include(p => p.Votes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poll == null) return NotFound();
            return View(poll);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, PollStatus status)
        {
            var poll = await _db.Polls.FindAsync(id);
            if (poll == null) return NotFound();

            poll.Status = status;
            poll.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Success"] = status switch
            {
                PollStatus.Active => "Oylama açıldı.",
                PollStatus.Closed => "Oylama kapatıldı.",
                _ => "Oylama taslağa alındı."
            };
            return RedirectToAction(nameof(Manage), new { id });
        }

        /// <summary>
        /// Oylamanın başlangıç/bitiş tarihlerini günceller. Oylama oluşturulduktan sonra tarih
        /// değiştirilemiyordu; ileri tarihli bir başlangıç girildiğinde oylamayı açmanın tek yolu
        /// silip yeniden oluşturmaktı. "Hemen başlat" (startNow) başlangıcı temizleyerek oylamayı
        /// anında maliklere açar.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSchedule(int id, DateTime? startsAt, DateTime? endsAt, bool startNow = false)
        {
            var poll = await _db.Polls.FindAsync(id);
            if (poll == null) return NotFound();

            if (startNow)
            {
                poll.StartsAt = null;
                // Bitiş tarihi geçmişse oylama yine kapalı kalırdı; bu durumda bitişi de temizleyelim.
                if (poll.EndsAt.HasValue && poll.EndsAt.Value < DateTime.Now)
                    poll.EndsAt = null;
            }
            else
            {
                if (startsAt.HasValue && endsAt.HasValue && endsAt.Value <= startsAt.Value)
                {
                    TempData["Error"] = "Bitiş tarihi başlangıçtan sonra olmalıdır.";
                    return RedirectToAction(nameof(Manage), new { id });
                }
                poll.StartsAt = startsAt;
                poll.EndsAt   = endsAt;
            }

            poll.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Success"] = startNow
                ? "Oylama hemen başlatıldı, malikler artık oy verebilir."
                : "Oylama tarihleri güncellendi.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> SetOptionImage(int optionId, IFormFile image)
        {
            var option = await _db.PollOptions.FirstOrDefaultAsync(o => o.Id == optionId);
            if (option == null) return NotFound();

            if (image == null || image.Length == 0)
            {
                TempData["Error"] = "Lütfen bir görsel seçin.";
                return RedirectToAction(nameof(Manage), new { id = option.PollId });
            }

            if (image.Length > MaxImageBytes)
            {
                TempData["Error"] = "Görsel 10 MB'ı aşamaz.";
                return RedirectToAction(nameof(Manage), new { id = option.PollId });
            }

            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!AllowedImageExt.Contains(ext))
            {
                TempData["Error"] = "Sadece JPG, PNG veya WEBP yükleyebilirsiniz.";
                return RedirectToAction(nameof(Manage), new { id = option.PollId });
            }

            var dir = Path.Combine(_env.WebRootPath, "uploads", "polls");
            Directory.CreateDirectory(dir);

            var fileName = $"{Guid.NewGuid()}{ext}";
            await using (var fs = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
                await image.CopyToAsync(fs);

            DeleteUploadedFile(option.ImagePath);
            option.ImagePath = $"/uploads/polls/{fileName}";
            await _db.SaveChangesAsync();

            TempData["Success"] = "Seçenek görseli güncellendi.";
            return RedirectToAction(nameof(Manage), new { id = option.PollId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOption(int id, string text)
        {
            var poll = await _db.Polls.FindAsync(id);
            if (poll == null) return NotFound();

            if (string.IsNullOrWhiteSpace(text))
            {
                TempData["Error"] = "Seçenek metni boş olamaz.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            var maxOrder = await _db.PollOptions.Where(o => o.PollId == id).MaxAsync(o => (int?)o.OrderIndex) ?? 0;

            _db.PollOptions.Add(new PollOption { PollId = id, Text = text.Trim(), OrderIndex = maxOrder + 1 });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Seçenek eklendi.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOption(int optionId)
        {
            var option = await _db.PollOptions.FirstOrDefaultAsync(o => o.Id == optionId);
            if (option == null) return NotFound();

            var pollId = option.PollId;

            // Bu seçeneğe verilmiş oy varsa silmeye izin verme (sonuçların bütünlüğü için).
            if (await _db.PollVotes.AnyAsync(v => v.PollOptionId == optionId))
            {
                TempData["Error"] = "Bu seçeneğe oy verilmiş, silinemez. Oylamayı kapatabilirsiniz.";
                return RedirectToAction(nameof(Manage), new { id = pollId });
            }

            DeleteUploadedFile(option.ImagePath);
            _db.PollOptions.Remove(option);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Seçenek silindi.";
            return RedirectToAction(nameof(Manage), new { id = pollId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var poll = await _db.Polls.Include(p => p.Options).FirstOrDefaultAsync(p => p.Id == id);
            if (poll == null) return NotFound();

            foreach (var o in poll.Options) DeleteUploadedFile(o.ImagePath);

            _db.Polls.Remove(poll);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Oylama silindi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task FillProjectsAsync()
        {
            ViewBag.Projects = await _db.Projects
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();
        }

        /// <summary>Yalnızca kendi yükleme klasörümüzdeki dosyayı siler.</summary>
        private void DeleteUploadedFile(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return;
            try
            {
                var rel = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(_env.WebRootPath, rel));
                var root = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "polls"));
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(full))
                    System.IO.File.Delete(full);
            }
            catch { /* yok sayılır */ }
        }
    }
}
