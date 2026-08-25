using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

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

        /// <summary>
        /// Yönetim ekranındaki tarihler ve "yeni seçenek ekle" alanını TEK bir "Kaydet" butonuyla
        /// aynı anda kaydeder. Önceden bu iki alan ayrı formlar/ayrı butonlardı (Tarihleri Kaydet,
        /// Ekle); admin tek bir "Oluştur"/"Kaydet" butonu arayınca kafası karışıyordu. newOptionText
        /// boş bırakılırsa sadece tarihler güncellenir, yeni seçenek eklenmez.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAll(int id, DateTime? startsAt, DateTime? endsAt, string? newOptionText)
        {
            var poll = await _db.Polls.FindAsync(id);
            if (poll == null) return NotFound();

            if (startsAt.HasValue && endsAt.HasValue && endsAt.Value <= startsAt.Value)
            {
                TempData["Error"] = "Bitiş tarihi başlangıçtan sonra olmalıdır.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            poll.StartsAt = startsAt;
            poll.EndsAt = endsAt;
            poll.UpdatedAt = DateTime.Now;

            var trimmedOption = (newOptionText ?? "").Trim();
            if (trimmedOption.Length > 0)
            {
                var maxOrder = await _db.PollOptions.Where(o => o.PollId == id).MaxAsync(o => (int?)o.OrderIndex) ?? 0;
                _db.PollOptions.Add(new PollOption { PollId = id, Text = trimmedOption, OrderIndex = maxOrder + 1 });
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Değişiklikler kaydedildi.";
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

            // Bu görsel, hem Admin hem Kat Maliki panelinde her zaman ~52-56px'lik küçük bir
            // yuvarlatılmış kare olarak gösteriliyor (bkz. Manage.cshtml, KatMaliki/Polls/Index.cshtml).
            // Öncesinde dosya olduğu gibi (telefon kamerasından çıkan 3-4000px, birkaç MB'lık
            // orijinal haliyle) kaydediliyordu — bu da oylama ekranının ilk açılışta yavaş
            // yüklenmesine sebep oluyordu. Burada da proje görselleri için kullanılan ImageSharp
            // ile küçültme uygulanıyor, ama hedef genişlik çok daha küçük (400px, retina ekranlar
            // için bile fazlasıyla yeterli).
            var fileName = $"{Guid.NewGuid()}.jpg";
            var fullPath = Path.Combine(dir, fileName);
            try
            {
                using var inputStream = image.OpenReadStream();
                using var img = await Image.LoadAsync(inputStream);
                if (img.Width > 400)
                    img.Mutate(x => x.Resize(400, 0));
                await img.SaveAsJpegAsync(fullPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 });
            }
            catch (Exception)
            {
                TempData["Error"] = "Geçersiz görsel dosyası.";
                return RedirectToAction(nameof(Manage), new { id = option.PollId });
            }

            DeleteUploadedFile(option.ImagePath);
            option.ImagePath = $"/uploads/polls/{fileName}";
            await _db.SaveChangesAsync();

            TempData["Success"] = "Seçenek görseli güncellendi.";
            return RedirectToAction(nameof(Manage), new { id = option.PollId });
        }

        /// <summary>
        /// SetOptionImage artık yeni yüklenen görselleri otomatik küçültüyor, ama bu değişiklikten
        /// önce yüklenmiş görseller (ör. test oylamasındaki "Meşe" görseli) sunucuda hâlâ orijinal
        /// boyutuyla duruyor ve oylama ekranını yavaşlatmaya devam ediyor. Bu işlem tüm mevcut
        /// seçenek görsellerini tek seferde tarayıp 400px'den büyükse küçültür — admin yeniden
        /// yüklemek zorunda kalmadan.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> OptimizeImages()
        {
            var options = await _db.PollOptions
                .Where(o => o.ImagePath != null && o.ImagePath != "")
                .ToListAsync();

            int optimized = 0, skipped = 0, failed = 0;

            foreach (var o in options)
            {
                try
                {
                    var rel = o.ImagePath!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var full = Path.GetFullPath(Path.Combine(_env.WebRootPath, rel));
                    var root = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "polls"));
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
                    {
                        skipped++;
                        continue;
                    }

                    using var img = await Image.LoadAsync(full);
                    if (img.Width <= 400)
                    {
                        skipped++;
                        continue;
                    }

                    img.Mutate(x => x.Resize(400, 0));
                    // Orijinal uzantıyla aynı formatta kaydet (ImageSharp encoder'ı dosya uzantısından
                    // seçer) — böylece .png bir dosya JPEG içerikle yanlış etiketlenmiş olmaz.
                    var tmpPath = Path.Combine(Path.GetDirectoryName(full)!, Path.GetFileNameWithoutExtension(full) + "_tmp" + Path.GetExtension(full));
                    await img.SaveAsync(tmpPath);
                    System.IO.File.Delete(full);
                    System.IO.File.Move(tmpPath, full);
                    optimized++;
                }
                catch
                {
                    failed++;
                }
            }

            TempData["Success"] = $"{optimized} görsel küçültüldü, {skipped} zaten uygundu, {failed} başarısız.";
            return RedirectToAction(nameof(Index));
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
