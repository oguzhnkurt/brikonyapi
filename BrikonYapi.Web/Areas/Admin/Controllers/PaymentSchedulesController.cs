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

        // Ünite/fon borçlusu seçilmemişse seçim ekranını göster (Bağımsız Bölümler / Fon Ödemeleri sekmeleri)
        public async Task<IActionResult> Index(int? unitId, int? projectId, int? fundDebtorId)
        {
            if (fundDebtorId != null)
            {
                var debtor = await _db.FundDebtors.Include(f => f.Project).FirstOrDefaultAsync(f => f.Id == fundDebtorId);
                if (debtor == null) return NotFound();

                var fundSchedules = await _db.PaymentSchedules
                    .Include(p => p.Transactions)
                    .Include(p => p.ProjectStage)
                    .Where(p => p.FundDebtorId == fundDebtorId)
                    .OrderBy(p => p.DueDate)
                    .ToListAsync();

                ViewBag.FundDebtor = debtor;
                return View(fundSchedules);
            }

            if (unitId == null)
            {
                var query = _db.Units.Include(u => u.Project).Include(u => u.Owner).AsQueryable();
                if (projectId.HasValue) query = query.Where(u => u.ProjectId == projectId);
                var units = await query.OrderBy(u => u.Project!.Name).ThenBy(u => u.UnitNo).ToListAsync();

                var unitIds = units.Select(u => u.Id).ToList();
                // Her bölümün ödeme planı özetini (kaç kalem, kaçı ödendi/gecikti) tek sorguda çıkarıp
                // seçim ekranında "plan var mı, durumu ne" bilgisini doğrudan gösterebilmek için.
                var statsRaw = await _db.PaymentSchedules
                    .Where(s => s.UnitId != null && unitIds.Contains(s.UnitId.Value))
                    .GroupBy(s => s.UnitId!.Value)
                    .Select(g => new
                    {
                        UnitId = g.Key,
                        Total = g.Count(),
                        Paid = g.Count(s => s.Status == PaymentScheduleStatus.Paid),
                        Overdue = g.Count(s => s.Status == PaymentScheduleStatus.Overdue)
                    })
                    .ToListAsync();
                var stats = statsRaw.ToDictionary(x => x.UnitId, x => (Total: x.Total, Paid: x.Paid, Overdue: x.Overdue));

                // Fon borçluları (ör. Arsa Sahibi) listesi ve plan özetleri.
                var fundQuery = _db.FundDebtors.Include(f => f.Project).AsQueryable();
                if (projectId.HasValue) fundQuery = fundQuery.Where(f => f.ProjectId == projectId);
                var fundDebtors = await fundQuery.OrderBy(f => f.Project!.Name).ThenBy(f => f.Name).ToListAsync();

                var debtorIds = fundDebtors.Select(f => f.Id).ToList();
                var fundStatsRaw = await _db.PaymentSchedules
                    .Where(s => s.FundDebtorId != null && debtorIds.Contains(s.FundDebtorId.Value))
                    .GroupBy(s => s.FundDebtorId!.Value)
                    .Select(g => new
                    {
                        FundDebtorId = g.Key,
                        Total = g.Count(),
                        Paid = g.Count(s => s.Status == PaymentScheduleStatus.Paid),
                        Overdue = g.Count(s => s.Status == PaymentScheduleStatus.Overdue)
                    })
                    .ToListAsync();
                var fundStats = fundStatsRaw.ToDictionary(x => x.FundDebtorId, x => (Total: x.Total, Paid: x.Paid, Overdue: x.Overdue));

                ViewBag.Stats = stats;
                ViewBag.FundDebtors = fundDebtors;
                ViewBag.FundStats = fundStats;
                ViewBag.Projects = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    await _db.Projects.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", projectId);
                ViewBag.SelectedProjectId = projectId;
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

        /// <summary>Yeni bir fon borçlusu (ör. Arsa Sahibi) tanımlar ve doğrudan taksit sihirbazına yönlendirir.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFundDebtor(int projectId, string name)
        {
            var project = await _db.Projects.FindAsync(projectId);
            if (project == null || string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Proje ve Malik/Borçlu adı gerekli.";
                return RedirectToAction(nameof(Index), new { projectId });
            }

            var debtor = new FundDebtor
            {
                ProjectId = projectId,
                FundType = PaymentFundType.ArsaSahibi,
                Name = name.Trim(),
                CreatedAt = DateTime.Now
            };
            _db.FundDebtors.Add(debtor);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Create), new { fundDebtorId = debtor.Id });
        }

        /// <summary>Bir fon borçlusunu ve varsa buna bağlı tüm ödeme kalemlerini siler (cascade).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFundDebtor(int id)
        {
            var debtor = await _db.FundDebtors.FindAsync(id);
            if (debtor == null) return NotFound();

            _db.FundDebtors.Remove(debtor);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Fon borçlusu silindi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Create(int? unitId, int? fundDebtorId)
        {
            if (fundDebtorId != null)
            {
                var debtor = await _db.FundDebtors.Include(f => f.Project).FirstOrDefaultAsync(f => f.Id == fundDebtorId);
                if (debtor == null) return NotFound();
                ViewBag.FundDebtor = debtor;
                ViewBag.Stages = await _db.ProjectStages
                    .Where(s => s.ProjectId == debtor.ProjectId)
                    .OrderBy(s => s.OrderIndex)
                    .ToListAsync();
                return View(new PaymentSchedule { FundDebtorId = fundDebtorId, DueDate = DateTime.Today.AddMonths(1) });
            }

            if (unitId == null) return NotFound();
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
        /// unitId veya fundDebtorId'den yalnızca biri gönderilir.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int? unitId, int? fundDebtorId, List<decimal> amount, List<DateTime> dueDate,
            List<string?> description, List<int?> hakedisPercentage, List<int?> installmentNo,
            List<int?> projectStageId, PaymentCurrency currency = PaymentCurrency.TRY)
        {
            Unit? unit = null;
            FundDebtor? debtor = null;
            int projectIdForStages;

            if (fundDebtorId != null)
            {
                debtor = await _db.FundDebtors.Include(f => f.Project).FirstOrDefaultAsync(f => f.Id == fundDebtorId);
                if (debtor == null) return NotFound();
                projectIdForStages = debtor.ProjectId;
            }
            else if (unitId != null)
            {
                unit = await _db.Units.Include(u => u.Project).Include(u => u.Owner).FirstOrDefaultAsync(u => u.Id == unitId);
                if (unit == null) return NotFound();
                projectIdForStages = unit.ProjectId;
            }
            else return NotFound();

            if (amount.Count == 0 || amount.Count != dueDate.Count)
            {
                TempData["Error"] = "En az bir taksit satırı girmelisiniz.";
                ViewBag.Unit = unit;
                ViewBag.FundDebtor = debtor;
                ViewBag.Stages = await _db.ProjectStages.Where(s => s.ProjectId == projectIdForStages).OrderBy(s => s.OrderIndex).ToListAsync();
                return View(new PaymentSchedule { UnitId = unitId, FundDebtorId = fundDebtorId, DueDate = DateTime.Today.AddMonths(1) });
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
                    UnitId = unit?.Id,
                    FundDebtorId = debtor?.Id,
                    Amount = amount[i],
                    Currency = currency,
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
            // Fon borçluları (arsa sahibi vb.) kayıtlı malik olmadığından bildirim gönderilmez.
            if (unit?.Owner != null)
            {
                foreach (var schedule in created)
                    await _notify.NotifyNewScheduleAsync(unit.Owner, schedule);
            }

            TempData["Success"] = created.Count == 1 ? "Ödeme kalemi eklendi." : $"{created.Count} ödeme kalemi eklendi.";
            return unit != null
                ? RedirectToAction(nameof(Index), new { unitId = unit.Id })
                : RedirectToAction(nameof(Index), new { fundDebtorId = debtor!.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var schedule = await _db.PaymentSchedules
                .Include(p => p.Unit).ThenInclude(u => u!.Project)
                .Include(p => p.FundDebtor).ThenInclude(f => f!.Project)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (schedule == null) return NotFound();

            int projectId;
            if (schedule.FundDebtor != null)
            {
                ViewBag.FundDebtor = schedule.FundDebtor;
                projectId = schedule.FundDebtor.ProjectId;
            }
            else
            {
                ViewBag.Unit = schedule.Unit;
                projectId = schedule.Unit!.ProjectId;
            }
            ViewBag.Stages = await _db.ProjectStages
                .Where(s => s.ProjectId == projectId)
                .OrderBy(s => s.OrderIndex)
                .ToListAsync();
            return View(schedule);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PaymentSchedule schedule)
        {
            var existing = await _db.PaymentSchedules
                .Include(p => p.Unit)
                .Include(p => p.FundDebtor)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();

            ModelState.Remove("Unit");
            ModelState.Remove("FundDebtor");
            ModelState.Remove(nameof(PaymentSchedule.Transactions));
            if (!ModelState.IsValid)
            {
                int? projId = existing.FundDebtor?.ProjectId ?? existing.Unit?.ProjectId;
                ViewBag.Unit = existing.Unit;
                ViewBag.FundDebtor = existing.FundDebtor;
                ViewBag.Stages = projId == null ? new List<ProjectStage>()
                    : await _db.ProjectStages.Where(s => s.ProjectId == projId).OrderBy(s => s.OrderIndex).ToListAsync();
                return View(schedule);
            }

            // Seçilen aşamanın geçerli olduğunu doğrula (yüzde artık elle girilen değerden gelir).
            ProjectStage? stage = schedule.ProjectStageId.HasValue
                ? await _db.ProjectStages.FirstOrDefaultAsync(s => s.Id == schedule.ProjectStageId.Value)
                : null;

            existing.Amount = schedule.Amount;
            existing.Currency = schedule.Currency;
            existing.DueDate = schedule.DueDate;
            existing.Description = schedule.Description;
            existing.Status = schedule.Status;
            existing.ProjectStageId = stage?.Id;
            existing.HakedisPercentage = schedule.HakedisPercentage;
            existing.InstallmentNo = schedule.InstallmentNo;
            existing.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Ödeme kalemi güncellendi.";
            return existing.UnitId != null
                ? RedirectToAction(nameof(Index), new { unitId = existing.UnitId })
                : RedirectToAction(nameof(Index), new { fundDebtorId = existing.FundDebtorId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var schedule = await _db.PaymentSchedules.FindAsync(id);
            if (schedule == null) return NotFound();
            var unitId = schedule.UnitId;
            var fundDebtorId = schedule.FundDebtorId;
            _db.PaymentSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Ödeme kalemi silindi.";
            return unitId != null
                ? RedirectToAction(nameof(Index), new { unitId })
                : RedirectToAction(nameof(Index), new { fundDebtorId });
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
            var sched = tx.PaymentSchedule;
            return sched?.UnitId != null
                ? RedirectToAction(nameof(Index), new { unitId = sched.UnitId })
                : RedirectToAction(nameof(Index), new { fundDebtorId = sched?.FundDebtorId });
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
            var sched = tx.PaymentSchedule;
            return sched?.UnitId != null
                ? RedirectToAction(nameof(Index), new { unitId = sched.UnitId })
                : RedirectToAction(nameof(Index), new { fundDebtorId = sched?.FundDebtorId });
        }
    }
}
