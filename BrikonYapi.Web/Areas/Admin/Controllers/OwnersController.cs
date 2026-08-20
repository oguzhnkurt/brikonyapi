using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class OwnersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public OwnersController(AppDbContext db, UserManager<IdentityUser> users)
        {
            _db    = db;
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var owners = await _db.Owners
                .Include(o => o.Units).ThenInclude(u => u.Project)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.AllProjects = await _db.Projects
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            // Temsil Heyeti: hangi malik hangi proje(ler)de üye — yıldız rozeti ve modal için.
            ViewBag.CommitteeAccess = await _db.OwnerProjectAccesses
                .Where(a => a.IsCommitteeMember)
                .Include(a => a.Project)
                .ToListAsync();

            return View(owners);
        }

        public IActionResult Create() => View(new Owner());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Owner owner, string email, string password)
        {
            ModelState.Remove("UserId");
            ModelState.Remove(nameof(Owner.Units));

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "E-posta ve şifre zorunludur.";
                return View(owner);
            }

            if (!ModelState.IsValid) return View(owner);

            var existingUser = await _users.FindByEmailAsync(email);
            if (existingUser != null)
            {
                TempData["Error"] = "Bu e-posta adresiyle zaten bir kullanıcı kayıtlı.";
                return View(owner);
            }

            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var createResult = await _users.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                TempData["Error"] = string.Join(" ", createResult.Errors.Select(e => e.Description));
                return View(owner);
            }

            await _users.AddToRoleAsync(user, "KatMaliki");

            owner.UserId = user.Id;
            owner.Email  = email;
            owner.CreatedAt = DateTime.Now;
            _db.Owners.Add(owner);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Kat maliki hesabı oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var owner = await _db.Owners.Include(o => o.Units).ThenInclude(u => u.Project).FirstOrDefaultAsync(o => o.Id == id);
            if (owner == null) return NotFound();

            ViewBag.AllProjects = await _db.Projects
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Access = await _db.OwnerProjectAccesses
                .Where(a => a.OwnerId == id)
                .ToListAsync();

            return View(owner);
        }

        /// <summary>
        /// Bu malikin hangi projelerin oylamalarını görebileceğini ve hangi proje sohbetine
        /// katılabileceğini admin tek tek belirler. Bağımsız bölüm sahipliğinden bağımsızdır.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAccess(int id, List<int>? seeProjectIds, List<int>? chatProjectIds)
        {
            var owner = await _db.Owners.FindAsync(id);
            if (owner == null) return NotFound();

            seeProjectIds  ??= new List<int>();
            chatProjectIds ??= new List<int>();
            var allProjectIds = seeProjectIds.Union(chatProjectIds).Distinct().ToList();

            var existing = await _db.OwnerProjectAccesses.Where(a => a.OwnerId == id).ToListAsync();

            // Artık ne görünürlük ne de sohbet için işaretli olmayan proje satırları tamamen kaldırılır.
            var toRemove = existing.Where(a => !allProjectIds.Contains(a.ProjectId)).ToList();
            if (toRemove.Count > 0) _db.OwnerProjectAccesses.RemoveRange(toRemove);

            foreach (var projectId in allProjectIds)
            {
                var row = existing.FirstOrDefault(a => a.ProjectId == projectId);
                if (row == null)
                {
                    row = new OwnerProjectAccess { OwnerId = id, ProjectId = projectId, CreatedAt = DateTime.Now };
                    _db.OwnerProjectAccesses.Add(row);
                }
                row.CanSeeProject = seeProjectIds.Contains(projectId);
                row.CanChat       = chatProjectIds.Contains(projectId);
                row.UpdatedAt     = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Proje ve sohbet erişimi güncellendi.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        /// <summary>
        /// Bu maliki, seçilen projenin Temsil Heyeti (kat malikleri temsilcisi) üyesi olarak işaretler.
        /// Erişim kaydı yoksa oluşturulur; proje görünürlüğü (CanSeeProject) otomatik açılır ki
        /// temsilci kendi temsil ettiği projeyi görebilsin.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignCommittee(int ownerId, int projectId)
        {
            var owner = await _db.Owners.FindAsync(ownerId);
            var project = await _db.Projects.FindAsync(projectId);
            if (owner == null || project == null) return NotFound();

            var row = await _db.OwnerProjectAccesses
                .FirstOrDefaultAsync(a => a.OwnerId == ownerId && a.ProjectId == projectId);

            if (row == null)
            {
                row = new OwnerProjectAccess { OwnerId = ownerId, ProjectId = projectId, CreatedAt = DateTime.Now };
                _db.OwnerProjectAccesses.Add(row);
            }

            row.IsCommitteeMember = true;
            row.CanSeeProject = true;
            row.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"{owner.FullName}, \"{project.Name}\" projesinin Temsil Heyeti üyesi olarak atandı.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Bu malikin, seçilen projedeki Temsil Heyeti üyeliğini kaldırır (erişim kaydı silinmez).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCommittee(int ownerId, int projectId)
        {
            var row = await _db.OwnerProjectAccesses
                .FirstOrDefaultAsync(a => a.OwnerId == ownerId && a.ProjectId == projectId);

            if (row != null)
            {
                row.IsCommitteeMember = false;
                row.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                TempData["Success"] = "Temsil Heyeti üyeliği kaldırıldı.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Owner owner)
        {
            var existing = await _db.Owners.FindAsync(id);
            if (existing == null) return NotFound();

            existing.FullName = owner.FullName;
            existing.Phone    = owner.Phone;
            existing.IsActive = owner.IsActive;
            existing.UpdatedAt = DateTime.Now;

            // Hesap pasife alınırsa girişini de kilitle
            var user = await _users.FindByIdAsync(existing.UserId);
            if (user != null)
                await _users.SetLockoutEndDateAsync(user, existing.IsActive ? null : DateTimeOffset.MaxValue);

            await _db.SaveChangesAsync();
            TempData["Success"] = "Kat maliki bilgileri güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Bağımsız bölümün kat / oda düzeni / metrekare bilgisi bu maliki düzenleme ekranından girilir
        /// (Kat Maliki Ana Sayfa kartında "3. Kat · 3+1 · 145 m²" gösterimi için).
        /// GÜVENLİK: bölüm, düzenlenen malike ait değilse işlem reddedilir.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUnitDetails(int ownerId, int unitId, int? floorNo, string? roomLayout, int? areaM2)
        {
            var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == unitId && u.OwnerId == ownerId);
            if (unit == null)
            {
                TempData["Error"] = "Bağımsız bölüm bulunamadı.";
                return RedirectToAction(nameof(Edit), new { id = ownerId });
            }

            unit.FloorNo = floorNo;
            unit.RoomLayout = string.IsNullOrWhiteSpace(roomLayout) ? null : roomLayout.Trim();
            unit.AreaM2 = areaM2;
            unit.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Bağımsız bölüm bilgileri güncellendi.";
            return RedirectToAction(nameof(Edit), new { id = ownerId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var owner = await _db.Owners.FindAsync(id);
            if (owner == null) return NotFound();

            var user = await _users.FindByIdAsync(owner.UserId);
            if (user == null) return NotFound();

            var newPassword = "Kat" + Random.Shared.Next(100000, 999999) + "!";
            var token  = await _users.GeneratePasswordResetTokenAsync(user);
            var result = await _users.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
                TempData["Success"] = $"Yeni şifre: {newPassword} (bu şifreyi malike güvenli bir şekilde iletin, tekrar gösterilmeyecektir).";
            else
                TempData["Error"] = "Şifre sıfırlanamadı: " + string.Join(" ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }
    }
}
