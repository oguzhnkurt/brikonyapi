using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Models.ViewModels;
using BrikonYapi.Web.Services;
using ClosedXML.Excel;
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

        // ── Excel ile toplu kat maliki ekleme ────────────────────

        public IActionResult BulkImport() => View(new OwnerBulkImportResult());

        /// <summary>Doldurulup geri yüklenecek örnek Excel şablonunu indirir.</summary>
        public IActionResult BulkTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Kat Malikleri");

            ws.Cell(1, 1).Value = "Ad Soyad";
            ws.Cell(1, 2).Value = "E-posta";
            ws.Cell(1, 3).Value = "Telefon";

            var header = ws.Range(1, 1, 1, 3);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F4FB");

            // Örnek satırlar — kullanıcı bunların üzerine yazar.
            ws.Cell(2, 1).Value = "Ahmet Yılmaz";
            ws.Cell(2, 2).Value = "ahmet.yilmaz@ornek.com";
            ws.Cell(2, 3).Value = "0532 000 00 00";

            ws.Cell(3, 1).Value = "Ayşe Demir";
            ws.Cell(3, 2).Value = "ayse.demir@ornek.com";
            ws.Cell(3, 3).Value = "0533 111 11 11";

            ws.Columns(1, 3).AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "kat-malikleri-sablon.xlsx");
        }

        /// <summary>
        /// Yüklenen Excel'deki her satır için bir kat maliki hesabı açar ve şifresini üretir.
        /// Hatalı satırlar atlanır, diğerleri işlenmeye devam eder; sonuç ekranında satır satır raporlanır.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> BulkImport(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Lütfen bir Excel dosyası seçin.";
                return RedirectToAction(nameof(BulkImport));
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx")
            {
                TempData["Error"] = "Sadece .xlsx uzantılı Excel dosyası yükleyebilirsiniz.";
                return RedirectToAction(nameof(BulkImport));
            }

            var result = new OwnerBulkImportResult();

            try
            {
                using var stream = file.OpenReadStream();
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheets.First();

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                // 1. satır başlık kabul edilir.
                for (var r = 2; r <= lastRow; r++)
                {
                    var fullName = ws.Cell(r, 1).GetString().Trim();
                    var email    = ws.Cell(r, 2).GetString().Trim();
                    var phone    = ws.Cell(r, 3).GetString().Trim();

                    // Tamamen boş satırları sessizce atla (Excel'in sonundaki boşluklar).
                    if (fullName.Length == 0 && email.Length == 0 && phone.Length == 0) continue;

                    var row = new OwnerImportRow
                    {
                        RowNumber = r,
                        FullName  = fullName,
                        Email     = email,
                        Phone     = string.IsNullOrWhiteSpace(phone) ? null : phone
                    };
                    result.Rows.Add(row);

                    if (fullName.Length == 0) { row.Error = "Ad Soyad boş."; continue; }
                    if (email.Length == 0)    { row.Error = "E-posta boş."; continue; }
                    if (!email.Contains('@') || email.Contains(' '))
                    {
                        row.Error = "E-posta geçersiz görünüyor.";
                        continue;
                    }

                    if (await _users.FindByEmailAsync(email) != null)
                    {
                        row.Error = "Bu e-posta ile zaten bir kullanıcı var.";
                        continue;
                    }

                    var password = OwnerPasswordGenerator.Generate();
                    var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                    var createResult = await _users.CreateAsync(user, password);
                    if (!createResult.Succeeded)
                    {
                        row.Error = string.Join(" ", createResult.Errors.Select(e => e.Description));
                        continue;
                    }

                    await _users.AddToRoleAsync(user, "KatMaliki");

                    _db.Owners.Add(new Owner
                    {
                        FullName  = fullName,
                        Email     = email,
                        Phone     = row.Phone,
                        UserId    = user.Id,
                        IsActive  = true,
                        CreatedAt = DateTime.Now
                    });
                    await _db.SaveChangesAsync();

                    row.Password = password;
                    row.Success  = true;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Excel dosyası okunamadı: " + ex.Message;
                return RedirectToAction(nameof(BulkImport));
            }

            if (result.Rows.Count == 0)
            {
                TempData["Error"] = "Dosyada işlenecek satır bulunamadı. İlk satır başlık olmalı, veriler 2. satırdan başlamalı.";
                return RedirectToAction(nameof(BulkImport));
            }

            // Şifre listesini indirebilmek için sonucu tek kullanımlık olarak sakla.
            TempData["ImportPasswords"] = System.Text.Json.JsonSerializer.Serialize(
                result.Rows.Where(x => x.Success).Select(x => new { x.FullName, x.Email, x.Password }));

            return View(result);
        }

        /// <summary>Toplu ekleme sonrası üretilen şifre listesini Excel olarak indirir.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DownloadPasswords(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return RedirectToAction(nameof(Index));

            var rows = System.Text.Json.JsonSerializer.Deserialize<List<PasswordRow>>(payload) ?? new();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Giriş Bilgileri");

            ws.Cell(1, 1).Value = "Ad Soyad";
            ws.Cell(1, 2).Value = "E-posta (kullanıcı adı)";
            ws.Cell(1, 3).Value = "Şifre";

            var header = ws.Range(1, 1, 1, 3);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F4FB");

            for (var i = 0; i < rows.Count; i++)
            {
                ws.Cell(i + 2, 1).Value = rows[i].FullName;
                ws.Cell(i + 2, 2).Value = rows[i].Email;
                ws.Cell(i + 2, 3).Value = rows[i].Password;
            }

            ws.Columns(1, 3).AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"kat-maliki-giris-bilgileri-{DateTime.Now:yyyy-MM-dd}.xlsx");
        }

        private class PasswordRow
        {
            public string FullName { get; set; } = "";
            public string Email    { get; set; } = "";
            public string Password { get; set; } = "";
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
