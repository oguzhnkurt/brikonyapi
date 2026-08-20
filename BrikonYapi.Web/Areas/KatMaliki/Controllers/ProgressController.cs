using BrikonYapi.Web.Areas.KatMaliki.Models;
using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.KatMaliki.Controllers
{
    /// <summary>Kat Maliki Portalı "İlerleme" ekranı: inşaat aşamaları, saha fotoğrafları ve 360° sanal tur.</summary>
    [Area("KatMaliki"), Authorize(Roles = "KatMaliki")]
    public class ProgressController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public ProgressController(AppDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _users.GetUserId(User);
            if (userId == null) return Challenge();

            var owner = await _db.Owners
                .Include(o => o.Units).ThenInclude(u => u.Project)
                .Include(o => o.Units).ThenInclude(u => u.PaymentSchedules)
                .FirstOrDefaultAsync(o => o.UserId == userId);

            if (owner == null) return NotFound("Bu kullanıcıya bağlı bir kat maliki profili bulunamadı.");

            // GÜVENLİK: Malik yalnızca kendi bağımsız bölümünün bulunduğu projelerin verisini görebilir.
            var projectIds = owner.Units
                .Where(u => u.IsActive)
                .Select(u => u.ProjectId)
                .Distinct()
                .ToList();

            var allSchedules = owner.Units.SelectMany(u => u.PaymentSchedules).ToList();
            var model = new ProgressPageViewModel
            {
                Owner = owner,
                HasAnySchedule = allSchedules.Count > 0,
                PaidTotal = allSchedules.Where(s => s.Status == PaymentScheduleStatus.Paid).Sum(s => s.Amount),
                RemainingTotal = allSchedules
                    .Where(s => s.Status == PaymentScheduleStatus.Pending || s.Status == PaymentScheduleStatus.Overdue)
                    .Sum(s => s.Amount)
            };

            model.RecentNews = await _db.Announcements
                .Where(a => a.IsActive && a.ShowOnHome)
                .OrderByDescending(a => a.OrderIndex)
                .ThenByDescending(a => a.CreatedAt)
                .Take(2)
                .ToListAsync();

            if (projectIds.Count == 0) return View(model);

            var stages = await _db.ProjectStages
                .Where(s => projectIds.Contains(s.ProjectId))
                .OrderBy(s => s.OrderIndex)
                .ToListAsync();

            var photos = await _db.SitePhotos
                .Where(p => projectIds.Contains(p.ProjectId) && p.IsActive)
                .OrderByDescending(p => p.TakenAt)
                .ThenBy(p => p.OrderIndex)
                .Take(60)
                .ToListAsync();

            foreach (var projectId in projectIds)
            {
                var units = owner.Units.Where(u => u.ProjectId == projectId && u.IsActive).ToList();
                var project = units.FirstOrDefault()?.Project;
                if (project == null) continue;

                // Proje geneli tanımlı değilse malikin bölümlerinin ortalamasını kullan.
                var overall = project.OverallProgressPercentage > 0
                    ? project.OverallProgressPercentage
                    : (units.Count > 0 ? (int)Math.Round(units.Average(u => u.ProgressPercentage)) : 0);

                model.Projects.Add(new ProjectProgressViewModel
                {
                    Project = project,
                    Units = units,
                    Stages = stages.Where(s => s.ProjectId == projectId).ToList(),
                    Photos = photos.Where(p => p.ProjectId == projectId).ToList(),
                    OverallProgress = Math.Clamp(overall, 0, 100)
                });
            }

            return View(model);
        }
    }
}
