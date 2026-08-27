using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>Proje bazlı, tekrar kullanılabilir "Ödeme Planı Şablonu" tanımları ve bunların bir
    /// projedeki bağımsız bölüm grubuna toplu uygulanması (Assign). Hakediş/aşama bazlı (Pursantaj)
    /// ve takvim/aylık bazlı olmak üzere iki şablon türünü destekler.</summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class PaymentPlanTemplatesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly PaymentNotificationService _notify;
        public PaymentPlanTemplatesController(AppDbContext db, PaymentNotificationService notify)
        {
            _db = db;
            _notify = notify;
        }

        // ── Liste ────────────────────────────────────────────────
        // unitId dolu geldiğinde (Owner sayfasındaki "Ödeme Planı Şablonu Uygula" linki) liste otomatik
        // olarak o bölümün projesine filtrelenir ve her satırdaki "Uygula" linki o bölümü hedefler.
        public async Task<IActionResult> Index(int? projectId, int? unitId)
        {
            Unit? unit = null;
            if (unitId.HasValue)
            {
                unit = await _db.Units.Include(u => u.Project).Include(u => u.Owner).FirstOrDefaultAsync(u => u.Id == unitId);
                if (unit != null) projectId = unit.ProjectId;
            }

            ViewBag.Projects = new SelectList(await _db.Projects.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", projectId);
            ViewBag.SelectedProjectId = projectId;
            ViewBag.Unit = unit;

            var query = _db.PaymentPlanTemplates.Include(t => t.Project).Include(t => t.Items).AsQueryable();
            if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId);

            var templates = await query.OrderBy(t => t.Project!.Name).ThenBy(t => t.Name).ToListAsync();
            return View(templates);
        }

        // ── Oluştur ──────────────────────────────────────────────
        public async Task<IActionResult> Create(int projectId)
        {
            var project = await _db.Projects.FindAsync(projectId);
            if (project == null) return NotFound();

            ViewBag.Project = project;
            ViewBag.Stages = await _db.ProjectStages.Where(s => s.ProjectId == projectId).OrderBy(s => s.OrderIndex).ToListAsync();
            return View(new PaymentPlanTemplate { ProjectId = projectId, PlanType = PaymentPlanType.StageBased });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int projectId, string name, PaymentPlanType planType, PaymentCurrency currency, string? description,
            List<string> label, List<decimal> percentage, List<int?> projectStageId, List<int?> monthOffset)
        {
            var project = await _db.Projects.FindAsync(projectId);
            if (project == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name) || label == null || label.Count == 0 || label.Count != percentage.Count)
            {
                TempData["Error"] = "Şablon adı ve en az bir kalem girmelisiniz.";
                ViewBag.Project = project;
                ViewBag.Stages = await _db.ProjectStages.Where(s => s.ProjectId == projectId).OrderBy(s => s.OrderIndex).ToListAsync();
                return View(new PaymentPlanTemplate { ProjectId = projectId, Name = name ?? "", PlanType = planType, Currency = currency, Description = description });
            }

            var template = new PaymentPlanTemplate
            {
                ProjectId = projectId,
                Name = name.Trim(),
                PlanType = planType,
                Currency = currency,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                CreatedAt = DateTime.Now
            };

            for (var i = 0; i < label.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(label[i]) || i >= percentage.Count || percentage[i] <= 0) continue;
                template.Items.Add(new PaymentPlanTemplateItem
                {
                    OrderIndex = i + 1,
                    Label = label[i].Trim(),
                    Percentage = percentage[i],
                    ProjectStageId = planType == PaymentPlanType.StageBased && i < projectStageId.Count ? projectStageId[i] : null,
                    MonthOffset = planType == PaymentPlanType.CalendarBased && i < monthOffset.Count ? monthOffset[i] : null,
                    CreatedAt = DateTime.Now
                });
            }

            if (template.Items.Count == 0)
            {
                TempData["Error"] = "En az bir geçerli kalem (başlık + yüzde) girmelisiniz.";
                ViewBag.Project = project;
                ViewBag.Stages = await _db.ProjectStages.Where(s => s.ProjectId == projectId).OrderBy(s => s.OrderIndex).ToListAsync();
                return View(new PaymentPlanTemplate { ProjectId = projectId, Name = name, PlanType = planType, Currency = currency, Description = description });
            }

            _db.PaymentPlanTemplates.Add(template);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Ödeme planı şablonu oluşturuldu.";
            return RedirectToAction(nameof(Index), new { projectId });
        }

        // ── Düzenle ──────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            var template = await _db.PaymentPlanTemplates
                .Include(t => t.Project)
                .Include(t => t.Items.OrderBy(i => i.OrderIndex))
                .FirstOrDefaultAsync(t => t.Id == id);
            if (template == null) return NotFound();

            ViewBag.Project = template.Project;
            ViewBag.Stages = await _db.ProjectStages.Where(s => s.ProjectId == template.ProjectId).OrderBy(s => s.OrderIndex).ToListAsync();
            return View(template);
        }

        /// <summary>Kalemler her düzenlemede baştan oluşturulur (mevcutlar silinip yeniden eklenir) —
        /// şablonlar seyrek değiştiği ve satır bazlı diff karmaşıklığına değmediği için basit ve güvenilir
        /// bir yaklaşımdır.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id, string name, PaymentPlanType planType, PaymentCurrency currency, string? description,
            List<string> label, List<decimal> percentage, List<int?> projectStageId, List<int?> monthOffset)
        {
            var template = await _db.PaymentPlanTemplates.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
            if (template == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name) || label == null || label.Count == 0 || label.Count != percentage.Count)
            {
                TempData["Error"] = "Şablon adı ve en az bir kalem girmelisiniz.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            var newItems = new List<PaymentPlanTemplateItem>();
            for (var i = 0; i < label.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(label[i]) || i >= percentage.Count || percentage[i] <= 0) continue;
                newItems.Add(new PaymentPlanTemplateItem
                {
                    PaymentPlanTemplateId = template.Id,
                    OrderIndex = newItems.Count + 1,
                    Label = label[i].Trim(),
                    Percentage = percentage[i],
                    ProjectStageId = planType == PaymentPlanType.StageBased && i < projectStageId.Count ? projectStageId[i] : null,
                    MonthOffset = planType == PaymentPlanType.CalendarBased && i < monthOffset.Count ? monthOffset[i] : null,
                    CreatedAt = DateTime.Now
                });
            }

            if (newItems.Count == 0)
            {
                TempData["Error"] = "En az bir geçerli kalem (başlık + yüzde) girmelisiniz.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            template.Name = name.Trim();
            template.PlanType = planType;
            template.Currency = currency;
            template.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            template.UpdatedAt = DateTime.Now;

            _db.PaymentPlanTemplateItems.RemoveRange(template.Items);
            _db.PaymentPlanTemplateItems.AddRange(newItems);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Ödeme planı şablonu güncellendi.";
            return RedirectToAction(nameof(Index), new { projectId = template.ProjectId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var template = await _db.PaymentPlanTemplates.FindAsync(id);
            if (template == null) return RedirectToAction(nameof(Index));

            var projectId = template.ProjectId;
            _db.PaymentPlanTemplates.Remove(template);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Şablon silindi.";
            return RedirectToAction(nameof(Index), new { projectId });
        }

        // ── Toplu / Tekil Atama ──────────────────────────────────
        // unitId verilirse yalnız o bölüme atama yapılır (Owner sayfasından manuel atama);
        // verilmezse projedeki tüm aktif bölümler arasından seçim yapılabilir (toplu atama).
        public async Task<IActionResult> Assign(int templateId, int? unitId)
        {
            var template = await _db.PaymentPlanTemplates
                .Include(t => t.Project)
                .Include(t => t.Items.OrderBy(i => i.OrderIndex)).ThenInclude(i => i.ProjectStage)
                .FirstOrDefaultAsync(t => t.Id == templateId);
            if (template == null) return NotFound();

            ViewBag.Template = template;

            if (unitId.HasValue)
            {
                var lockedUnit = await _db.Units.Include(u => u.Owner).Include(u => u.Project)
                    .FirstOrDefaultAsync(u => u.Id == unitId && u.ProjectId == template.ProjectId);
                if (lockedUnit == null) return NotFound();
                ViewBag.LockedUnit = lockedUnit;
                ViewBag.Units = new List<Unit> { lockedUnit };
            }
            else
            {
                ViewBag.LockedUnit = null;
                ViewBag.Units = await _db.Units.Include(u => u.Owner).Include(u => u.Project)
                    .Where(u => u.ProjectId == template.ProjectId && u.IsActive)
                    .OrderBy(u => u.UnitNo).ToListAsync();
            }

            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int templateId, List<int> unitIds, DateTime assignDate)
        {
            var template = await _db.PaymentPlanTemplates
                .Include(t => t.Items).ThenInclude(i => i.ProjectStage)
                .FirstOrDefaultAsync(t => t.Id == templateId);
            if (template == null) return NotFound();

            if (unitIds == null || unitIds.Count == 0)
            {
                TempData["Error"] = "En az bir bağımsız bölüm seçmelisiniz.";
                return RedirectToAction(nameof(Assign), new { templateId });
            }

            var units = await _db.Units.Include(u => u.Owner)
                .Where(u => unitIds.Contains(u.Id) && u.ProjectId == template.ProjectId)
                .ToListAsync();

            var skipped = new List<string>();
            var appliedCount = 0;
            var scheduleCount = 0;
            var orderedItems = template.Items.OrderBy(i => i.OrderIndex).ToList();

            foreach (var unit in units)
            {
                if (!unit.ContractAmount.HasValue || unit.ContractAmount.Value <= 0)
                {
                    skipped.Add(unit.UnitNo);
                    continue;
                }

                var created = new List<PaymentSchedule>();
                foreach (var item in orderedItems)
                {
                    var amount = Math.Round(unit.ContractAmount.Value * item.Percentage / 100m, 2);
                    if (amount <= 0) continue;

                    DateTime dueDate;
                    if (template.PlanType == PaymentPlanType.CalendarBased)
                    {
                        dueDate = assignDate.AddMonths(item.MonthOffset ?? 0);
                    }
                    else
                    {
                        // Hakediş/aşama bazlı kalemlerde asıl tetikleyici aşamanın tamamlanmasıdır;
                        // takvimsel bir vade yoktur. Aşamanın planlanan bitiş tarihi varsa onu, yoksa
                        // atama tarihini bilgi amaçlı vade olarak kullanırız — admin sonradan elle değiştirebilir.
                        dueDate = item.ProjectStage?.PlannedEndDate ?? assignDate;
                    }

                    var schedule = new PaymentSchedule
                    {
                        UnitId = unit.Id,
                        Amount = amount,
                        Currency = template.Currency,
                        DueDate = dueDate,
                        Description = item.Label,
                        HakedisPercentage = (int)Math.Round(item.Percentage),
                        ProjectStageId = item.ProjectStageId,
                        InstallmentNo = item.OrderIndex,
                        CreatedAt = DateTime.Now
                    };
                    _db.PaymentSchedules.Add(schedule);
                    created.Add(schedule);
                }

                if (created.Count > 0)
                {
                    await _db.SaveChangesAsync(); // Id'ler dolsun ki bildirim kaydı ilişkilendirilebilsin
                    appliedCount++;
                    scheduleCount += created.Count;

                    if (unit.Owner != null)
                    {
                        foreach (var schedule in created)
                            await _notify.NotifyNewScheduleAsync(unit.Owner, schedule);
                    }
                }
            }

            if (skipped.Count > 0)
                TempData["Error"] = $"Toplam ödeme tutarı (Toplam Ödeme alanı) tanımlanmamış bölümler atlandı: {string.Join(", ", skipped)}";

            if (appliedCount > 0)
                TempData["Success"] = $"{appliedCount} bölüme, toplam {scheduleCount} taksit içeren ödeme planı uygulandı.";

            return RedirectToAction(nameof(Index), new { projectId = template.ProjectId });
        }
    }
}
