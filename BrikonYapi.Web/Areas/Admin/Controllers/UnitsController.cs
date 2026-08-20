using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class UnitsController : Controller
    {
        private readonly AppDbContext _db;
        public UnitsController(AppDbContext db) => _db = db;

        private async Task LoadDropdownsAsync(int? selectedProjectId = null, int? selectedOwnerId = null)
        {
            ViewBag.Projects = new SelectList(
                await _db.Projects.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", selectedProjectId);
            ViewBag.Owners = new SelectList(
                await _db.Owners.Where(o => o.IsActive).OrderBy(o => o.FullName).ToListAsync(), "Id", "FullName", selectedOwnerId);
        }

        public async Task<IActionResult> Index(int? projectId)
        {
            var query = _db.Units.Include(u => u.Project).Include(u => u.Owner).AsQueryable();
            if (projectId.HasValue) query = query.Where(u => u.ProjectId == projectId);

            ViewBag.Projects = new SelectList(await _db.Projects.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", projectId);
            ViewBag.SelectedProjectId = projectId;

            var units = await query.OrderBy(u => u.Project!.Name).ThenBy(u => u.UnitNo).ToListAsync();
            return View(units);
        }

        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();
            return View(new Unit());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Unit unit)
        {
            ModelState.Remove("Project");
            ModelState.Remove("Owner");
            ModelState.Remove(nameof(Unit.PaymentSchedules));

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(unit.ProjectId, unit.OwnerId);
                return View(unit);
            }

            if (await _db.Units.AnyAsync(u => u.ProjectId == unit.ProjectId && u.UnitNo == unit.UnitNo))
            {
                TempData["Error"] = "Bu projede aynı numaralı bağımsız bölüm zaten mevcut.";
                await LoadDropdownsAsync(unit.ProjectId, unit.OwnerId);
                return View(unit);
            }

            unit.CreatedAt = DateTime.Now;
            if (unit.OwnerId == 0) unit.OwnerId = null;
            _db.Units.Add(unit);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Bağımsız bölüm eklendi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var unit = await _db.Units.FindAsync(id);
            if (unit == null) return NotFound();
            await LoadDropdownsAsync(unit.ProjectId, unit.OwnerId);
            return View(unit);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Unit unit)
        {
            var existing = await _db.Units.FindAsync(id);
            if (existing == null) return NotFound();

            ModelState.Remove("Project");
            ModelState.Remove("Owner");
            ModelState.Remove(nameof(Unit.PaymentSchedules));
            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(unit.ProjectId, unit.OwnerId);
                return View(unit);
            }

            if (existing.ProgressPercentage != unit.ProgressPercentage)
                existing.ProgressUpdatedAt = DateTime.Now;

            existing.ProjectId = unit.ProjectId;
            existing.UnitNo = unit.UnitNo;
            existing.BlockNo = unit.BlockNo;
            existing.FloorNo = unit.FloorNo;
            existing.RoomLayout = unit.RoomLayout;
            existing.AreaM2 = unit.AreaM2;
            existing.ProgressPercentage = Math.Clamp(unit.ProgressPercentage, 0, 100);
            existing.OwnerId = unit.OwnerId == 0 ? null : unit.OwnerId;
            existing.Notes = unit.Notes;
            existing.IsActive = unit.IsActive;
            existing.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Bağımsız bölüm güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var unit = await _db.Units.FindAsync(id);
            if (unit != null)
            {
                _db.Units.Remove(unit);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Bağımsız bölüm silindi.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
