using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class PaymentSchedulesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PaymentNotificationService _notify;
        public PaymentSchedulesController(AppDbContext db, PaymentNotificationService notify)
        {
            _db = db;
            _notify = notify;
        }

        // Ünite seçilmemişse tüm bağımsız bölümleri listele (seçim ekranı)
        public async Task<IActionResult> Index(int? unitId)
        {
            if (unitId == null)
            {
                var units = await _db.Units.Include(u => u.Project).Include(u => u.Owner)
                    .OrderBy(u => u.Project!.Name).ThenBy(u => u.UnitNo).ToListAsync();
                return View("SelectUnit", units);
            }

            var unit = await _db.Units.Include(u => u.Project).Include(u => u.Owner).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return NotFound();

            var schedules = await _db.PaymentSchedules
                .Include(p => p.Transactions)
                .Include(p => p.ProjectStage)
                .Where(p => p.UnitId == unitId)
                .OrderBy(p => p.DueDate)
                .ToListAsync();

            ViewBag.Unit = unit;
            return View(schedules);
        }

        public async Task<IActionResult> Create(int unitId)
        {
            var unit = await _db.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return NotFound();
            ViewBag.Unit = unit;
            ViewBag.Stages = await _db.ProjectStages
                .Where(s => s.ProjectId == unit.ProjectId)
                .OrderBy(s => s.OrderIndex)
                .ToListAsync();
            return View(new PaymentSchedule { UnitId = unitId, DueDate = DateTime.Today.AddMonths(1) });
        }

        /// <summary>
        /// Bir veya birden fazla taksidi tek seferde kaydeder ("+ Taksit Ekle" ile eklenen satırlar).
        /// Formdaki her alan aynı isimle tekrarlandığı için (amount, dueDate, ...) ASP.NET Core
        /// bunları otomatik olarak sıraya bağlı listelere bağlar — n. amount ile n. dueDate aynı satıra aittir.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int unitId, List<decimal> amount, List<DateTime> dueDate,
            List<string?> description, List<int?> hakedisPercentage, List<int?> installmentNo,
            List<int?> projectStageId)
        {
            var unit = await _db.Units.Include(u => u.Project).Include(u => u.Owner).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return NotFound();

            if (amount.Count == 0 || amount.Count != dueDate.Count)
            {
                TempData["Error"] = "En az bir taksit satırı girmelisiniz.";
                ViewBag.Unit = unit;
                ViewBag.Stages = await _db.ProjectStages.Where(s => s.ProjectId == unit.ProjectId).OrderBy(s => s.OrderIndex).ToListAsync();
                return View(new PaymentSchedule { UnitId = unitId, DueDate = DateTime.Today.AddMonths(1) });
            }

            // Seçilen aşamaların geçerliliğini önceden çekelim ki her satırda tekrar sorgu atmayalım.
            var stageIds = projectStageId.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var stages = stageIds.Count > 0
                ? await _db.ProjectStages.Where(s => stageIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id)
                : new Dictionary<int, ProjectStage>();

            var created = new List<PaymentSchedule>();
            for (var i = 0; i < amount.Count; i++)
            {
                if (amount[i] <= 0) continue;

                var stageId = i < projectStageId.Count ? projectStageId[i] : null;
                var stage = stageId.HasValue && stages.TryGetValue(stageId.Value, out var st) ? st : null;

                var schedule = new PaymentSchedule
                {
                    UnitId = unitId,
                    Amount = amount[i],
                    DueDate = dueDate[i],
                    Description = i < description.Count ? description[i] : null,
                    HakedisPercentage = i < hakedisPercentage.Count ? hakedisPercentage[i] : null,
                    ProjectStageId = stage?.Id,
                    InstallmentNo = i < installmentNo.Count ? (installmentNo[i] ?? 0) : 0,
                    CreatedAt = DateTime.Now
                };
                _db.PaymentSchedules.Add(schedule);
                created.Add(schedule);
            }

            await _db.SaveChangesAsync();

            // Malike, bölümünün sahibiyse, yeni tanımlanan her taksit için bilgilendirme gönder.
            if (unit.Owner != null)
            {
                foreach (var schedule in created)
                    await _notify.NotifyNewScheduleAsync(unit.Owner, schedule);
            }

            TempData["Success"] = created.Count == 1 ? "Ödeme kalemi eklendi." : $"{created.Count} ödeme kalemi eklendi.";
            return RedirectToAction(nameof(Index), new { unitId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var schedule = await _db.PaymentSchedules.Include(p => p.Unit).ThenInclude(u => u!.Project).FirstOrDefaultAsync(p => p.Id == id);
            if (schedule == null) return NotFound();
            ViewBag.Unit = schedule.Unit;
            ViewBag.Stages = await _db.ProjectStages
                .Where(s => s.ProjectId == schedule.Unit!.ProjectId)
                .OrderBy(s => s.OrderIndex)
                .ToListAsync();
            return View(schedule);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PaymentSchedule schedule)
        {
            var existing = await _db.PaymentSchedules.FindAsync(id);
            if (existing == null) return NotFound();

            ModelState.Remove("Unit");
            ModelState.Remove(nameof(PaymentSchedule.Transactions));
            if (!ModelState.IsValid)
            {
                var unitForView = await _db.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == existing.UnitId);
                ViewBag.Unit = unitForView;
                ViewBag.Stages = unitForView == null ? new List<ProjectStage>()
                    : await _db.ProjectStages.Where(s => s.ProjectId == unitForView.ProjectId).OrderBy(s => s.OrderIndex).ToListAsync();
                return View(schedule);
            }

            // Seçilen aşamanın geçerli olduğunu doğrula (yüzde artık elle girilen değerden gelir).
            ProjectStage? stage = schedule.ProjectStageId.HasValue
                ? await _db.ProjectStages.FirstOrDefaultAsync(s => s.Id == schedule.ProjectStageId.Value)
                : null;

            existing.Amount = schedule.Amount;
            existing.DueDate = schedule.DueDate;
            existing.Description = schedule.Description;
            existing.Status = schedule.Status;
            existing.ProjectStageId = stage?.Id;
            existing.HakedisPercentage = schedule.HakedisPercentage;
            existing.InstallmentNo = schedule.InstallmentNo;
            existing.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Ödeme kalemi güncellendi.";
            return RedirectToAction(nameof(Index), new { unitId = existing.UnitId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var schedule = await _db.PaymentSchedules.FindAsync(id);
            if (schedule == null) return NotFound();
            var unitId = schedule.UnitId;
            _db.PaymentSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Ödeme kalemi silindi.";
            return RedirectToAction(nameof(Index), new { unitId });
        }

        // ── Havale/EFT bildirimlerini onaylama ───────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTransaction(int transactionId)
        {
            var tx = await _db.PaymentTransactions.Include(t => t.PaymentSchedule).ThenInclude(s => s!.Unit).ThenInclude(u => u!.Owner)
                .FirstOrDefaultAsync(t => t.Id == transactionId);
            if (tx == null) return NotFound();

            tx.Status = PaymentTransactionStatus.Approved;
            tx.ApprovedAt = DateTime.Now;
            tx.ApprovedByUserId = User.Identity?.Name;

            if (tx.PaymentSchedule != null)
            {
                tx.PaymentSchedule.Status = PaymentScheduleStatus.Paid;
                tx.PaymentSchedule.UpdatedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync();

            var owner = tx.PaymentSchedule?.Unit?.Owner;
            if (owner != null && tx.PaymentSchedule != null)
                await _notify.NotifyTransactionApprovedAsync(owner, tx.PaymentSchedule);

            TempData["Success"] = "Ödeme onaylandı.";
            return RedirectToAction(nameof(Index), new { unitId = tx.PaymentSchedule?.UnitId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTransaction(int transactionId, string? note)
        {
            var tx = await _db.PaymentTransactions.Include(t => t.PaymentSchedule).ThenInclude(s => s!.Unit).ThenInclude(u => u!.Owner)
                .FirstOrDefaultAsync(t => t.Id == transactionId);
            if (tx == null) return NotFound();

            tx.Status = PaymentTransactionStatus.Rejected;
            tx.Note = note;
            await _db.SaveChangesAsync();

            var owner = tx.PaymentSchedule?.Unit?.Owner;
            if (owner != null && tx.PaymentSchedule != null)
                await _notify.NotifyTransactionRejectedAsync(owner, tx.PaymentSchedule, note);

            TempData["Success"] = "Bildirim reddedildi.";
            return RedirectToAction(nameof(Index), new { unitId = tx.PaymentSchedule?.UnitId });
        }
    }
}
