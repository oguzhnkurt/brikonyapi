using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.KatMaliki.Controllers
{
    /// <summary>Kat Maliki Portalı "Oylama" ekranı: malik anketleri ve malzeme seçim süreçleri.</summary>
    [Area("KatMaliki"), Authorize(Roles = "KatMaliki")]
    public class PollsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public PollsController(AppDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        private async Task<Owner?> GetOwnerAsync()
        {
            var userId = _users.GetUserId(User);
            if (userId == null) return null;
            return await _db.Owners.FirstOrDefaultAsync(o => o.UserId == userId);
        }

        /// <summary>
        /// Malikin oylama görebileceği projeler: admin'in bu malike açıkça "oylamayı görsün" dediği
        /// projeler (bağımsız bölüm sahipliğinden bağımsız — Admin &gt; Kat Malikleri &gt; Erişim'den atanır).
        /// </summary>
        private Task<List<int>> GetVisibleProjectIdsAsync(int ownerId) =>
            _db.OwnerProjectAccesses.Where(a => a.OwnerId == ownerId && a.CanSeeProject)
                .Select(a => a.ProjectId).ToListAsync();

        /// <summary>Malikin görebileceği oylamalar: kendisine atanmış projelere ait olanlar + tüm projelere açık olanlar.</summary>
        private static IQueryable<Poll> VisibleTo(IQueryable<Poll> query, List<int> projectIds) =>
            query.Where(p => p.ProjectId == null || projectIds.Contains(p.ProjectId.Value));

        public async Task<IActionResult> Index()
        {
            var owner = await GetOwnerAsync();
            if (owner == null) return NotFound("Bu kullanıcıya bağlı bir kat maliki profili bulunamadı.");

            var projectIds = await GetVisibleProjectIdsAsync(owner.Id);

            var polls = await VisibleTo(_db.Polls.AsQueryable(), projectIds)
                .Where(p => p.Status != PollStatus.Draft)
                .Include(p => p.Options)
                .Include(p => p.Votes)
                .OrderByDescending(p => p.Status == PollStatus.Active)
                .ThenByDescending(p => p.CreatedAt)
                .Take(50)
                .ToListAsync();

            ViewBag.OwnerId = owner.Id;
            return View(polls);
        }

        /// <summary>
        /// Fotoğrafa/karta tıklandığında AJAX ile çağrılır — ayrı bir "gönder" adımı yoktur.
        /// Malik daha önce oy kullanmışsa, oyu talep edilen seçeneğe GÜNCELLENİR (oy değiştirme serbest).
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Vote(int pollId, int optionId)
        {
            var owner = await GetOwnerAsync();
            if (owner == null) return Json(new { success = false, message = "Kat maliki profili bulunamadı." });

            var projectIds = await GetVisibleProjectIdsAsync(owner.Id);

            // GÜVENLİK 1: Oylama malikin erişimine açık projeler kapsamında mı?
            var poll = await VisibleTo(_db.Polls.AsQueryable(), projectIds)
                .Include(p => p.Options)
                .Include(p => p.Votes)
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll == null)
                return Json(new { success = false, message = "Oylama bulunamadı." });

            // GÜVENLİK 2: Oylama açık mı, tarih aralığında mı?
            var now = DateTime.Now;
            var open = poll.Status == PollStatus.Active
                       && (poll.StartsAt == null || poll.StartsAt <= now)
                       && (poll.EndsAt == null || poll.EndsAt >= now);

            if (!open)
            {
                // Henüz başlamamış bir oylama için "kapandı" demek yanıltıcı olur.
                var notStarted = poll.Status == PollStatus.Active && poll.StartsAt != null && poll.StartsAt > now;
                var message = notStarted
                    ? $"Bu oylama {poll.StartsAt!.Value:dd.MM.yyyy HH:mm} tarihinde başlayacak, henüz oy kullanılamaz."
                    : "Bu oylama kapanmıştır, oy kullanılamaz.";
                return Json(new { success = false, message });
            }

            // GÜVENLİK 3: Seçenek gerçekten bu oylamaya mı ait?
            if (!poll.Options.Any(o => o.Id == optionId))
                return Json(new { success = false, message = "Geçersiz seçenek." });

            var myVote = await _db.PollVotes.FirstOrDefaultAsync(v => v.PollId == pollId && v.OwnerId == owner.Id);

            try
            {
                if (myVote == null)
                {
                    _db.PollVotes.Add(new PollVote
                    {
                        PollId = pollId,
                        PollOptionId = optionId,
                        OwnerId = owner.Id,
                        CreatedAt = now
                    });
                }
                else
                {
                    // Oy değiştirme: aynı satır güncellenir, (PollId, OwnerId) benzersizliği bozulmaz.
                    myVote.PollOptionId = optionId;
                }

                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Aynı anda iki istek geldiyse benzersiz indeks devreye girer.
                return Json(new { success = false, message = "Oy kaydedilemedi, lütfen tekrar deneyin." });
            }

            var totalVotes = await _db.PollVotes.CountAsync(v => v.PollId == pollId);
            var counts = await _db.PollVotes
                .Where(v => v.PollId == pollId)
                .GroupBy(v => v.PollOptionId)
                .Select(g => new { OptionId = g.Key, Count = g.Count() })
                .ToListAsync();

            return Json(new
            {
                success = true,
                message = "Oyunuz kaydedildi.",
                pollId,
                myOptionId = optionId,
                totalVotes,
                options = counts.Select(c => new
                {
                    optionId = c.OptionId,
                    count = c.Count,
                    pct = totalVotes > 0 ? (int)Math.Round(c.Count * 100.0 / totalVotes) : 0
                })
            });
        }
    }
}
