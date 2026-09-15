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

        public async Task<IActionResult> Index(int? projectId)
        {
            var query = _db.Owners
                .Include(o => o.Units).ThenInclude(u => u.Project)
                .AsQueryable();

            if (projectId.HasValue)
                query = query.Where(o => o.Units.Any(u => u.ProjectId == projectId.Value));

            var owners = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var allProjects = await _db.Projects
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.AllProjects = allProjects;
            ViewBag.ProjectFilter = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(allProjects, "Id", "Name", projectId);
            ViewBag.SelectedProjectId = projectId;

            // Temsil Heyeti: hangi malik hangi proje(ler)de üye — yıldız rozeti ve modal için.
            ViewBag.CommitteeAccess = await _db.OwnerProjectAccesses
                .Where(a => a.IsCommitteeMember)
                .Include(a => a.Project)
                .ToListAsync();

            // Bildirim tercihleri: her malik için bir satır var ya da hiç yok (KatMaliki tarafında
            // "Profilim" ekranından ilk kayıt oluşturulmadıysa satır yok demektir) — view, eksik
            // olan malikler için varsayılan (hepsi açık, WhatsApp dahil) değerleri gösterecek.
            ViewBag.NotificationPrefs = await _db.OwnerNotificationPreferences.ToListAsync();

            return View(owners);
        }

        public IActionResult Create() => View(new Owner());

        /// <summary>Kat Maliki Portalı'na giriş artık telefon numarası (kullanıcı adı) + T.C. Kimlik No
        /// (şifre) ile yapılıyor — bkz. KatMaliki/AccountController.Login, PhoneNormalizer.
        /// E-posta artık sadece iletişim bilgisi, giriş için kullanılmıyor.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Owner owner)
        {
            ModelState.Remove("UserId");
            ModelState.Remove(nameof(Owner.Units));

            var normalizedPhone = PhoneNormalizer.Normalize(owner.Phone);
            if (normalizedPhone == null)
            {
                TempData["Error"] = "Telefon numarası geçersiz — 05XXXXXXXXX biçiminde, 11 haneli olmalı.";
                return View(owner);
            }

            if (!PhoneNormalizer.IsValidTcKimlikNo(owner.TcKimlikNo))
            {
                TempData["Error"] = "T.C. Kimlik No geçersiz — 11 haneli olmalı ve 0 ile başlayamaz.";
                return View(owner);
            }

            if (!ModelState.IsValid) return View(owner);

            var existingUser = await _users.FindByNameAsync(normalizedPhone);
            if (existingUser != null)
            {
                TempData["Error"] = "Bu telefon numarasıyla zaten bir kullanıcı kayıtlı.";
                return View(owner);
            }

            var user = new IdentityUser { UserName = normalizedPhone, Email = owner.Email, EmailConfirmed = true };
            var createResult = await _users.CreateAsync(user, owner.TcKimlikNo);
            if (!createResult.Succeeded)
            {
                TempData["Error"] = string.Join(" ", createResult.Errors.Select(e => e.Description));
                return View(owner);
            }

            await _users.AddToRoleAsync(user, "KatMaliki");

            owner.UserId = user.Id;
            owner.Phone  = normalizedPhone;
            owner.CreatedAt = DateTime.Now;
            _db.Owners.Add(owner);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Kat maliki hesabı oluşturuldu. Şimdi isterseniz aşağıdan bir bağımsız bölüm atayabilirsiniz.";
            // Doğrudan Edit ekranına yönlendiriyoruz ki bölüm ataması aynı akışın devamı gibi hissettirsin
            // (ayrı bir "Bağımsız Bölümler" ekranına gitmek zorunda kalınmasın).
            return RedirectToAction(nameof(Edit), new { id = owner.Id });
        }

        // ── Excel ile toplu kat maliki ekleme ────────────────────

        public IActionResult BulkImport() => View(new OwnerBulkImportResult());

        /// <summary>Doldurulup geri yüklenecek örnek Excel şablonunu indirir.</summary>
        public IActionResult BulkTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Kat Malikleri");

            ws.Cell(1, 1).Value = "Ad Soyad";
            ws.Cell(1, 2).Value = "Telefon (giriş için kullanıcı adı)";
            ws.Cell(1, 3).Value = "T.C. Kimlik No (giriş şifresi)";
            ws.Cell(1, 4).Value = "E-posta (opsiyonel)";

            var header = ws.Range(1, 1, 1, 4);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F4FB");

            // Örnek satırlar — kullanıcı bunların üzerine yazar.
            ws.Cell(2, 1).Value = "Ahmet Yılmaz";
            ws.Cell(2, 2).Value = "0532 000 00 00";
            ws.Cell(2, 3).Value = "12345678901";
            ws.Cell(2, 4).Value = "ahmet.yilmaz@ornek.com";

            ws.Cell(3, 1).Value = "Ayşe Demir";
            ws.Cell(3, 2).Value = "0533 111 11 11";
            ws.Cell(3, 3).Value = "98765432109";
            ws.Cell(3, 4).Value = "ayse.demir@ornek.com";

            ws.Columns(1, 4).AdjustToContents();

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
                // 1. satır başlık kabul edilir. Kolon sırası: Ad Soyad, Telefon, T.C. Kimlik No, E-posta(opsiyonel).
                for (var r = 2; r <= lastRow; r++)
                {
                    var fullName  = ws.Cell(r, 1).GetString().Trim();
                    var phoneRaw  = ws.Cell(r, 2).GetString().Trim();
                    var tcKimlik  = ws.Cell(r, 3).GetString().Trim();
                    var email     = ws.Cell(r, 4).GetString().Trim();

                    // Tamamen boş satırları sessizce atla (Excel'in sonundaki boşluklar).
                    if (fullName.Length == 0 && phoneRaw.Length == 0 && tcKimlik.Length == 0 && email.Length == 0) continue;

                    var row = new OwnerImportRow
                    {
                        RowNumber = r,
                        FullName  = fullName,
                        Email     = email,
                        Phone     = string.IsNullOrWhiteSpace(phoneRaw) ? null : phoneRaw
                    };
                    result.Rows.Add(row);

                    if (fullName.Length == 0) { row.Error = "Ad Soyad boş."; continue; }

                    var normalizedPhone = PhoneNormalizer.Normalize(phoneRaw);
                    if (normalizedPhone == null)
                    {
                        row.Error = "Telefon geçersiz — 05XXXXXXXXX biçiminde, 11 haneli olmalı.";
                        continue;
                    }

                    if (!PhoneNormalizer.IsValidTcKimlikNo(tcKimlik))
                    {
                        row.Error = "T.C. Kimlik No geçersiz — 11 haneli olmalı ve 0 ile başlayamaz.";
                        continue;
                    }

                    if (await _users.FindByNameAsync(normalizedPhone) != null)
                    {
                        row.Error = "Bu telefon numarasıyla zaten bir kullanıcı var.";
                        continue;
                    }

                    var user = new IdentityUser
                    {
                        UserName = normalizedPhone,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email,
                        EmailConfirmed = true
                    };
                    var createResult = await _users.CreateAsync(user, tcKimlik);
                    if (!createResult.Succeeded)
                    {
                        row.Error = string.Join(" ", createResult.Errors.Select(e => e.Description));
                        continue;
                    }

                    await _users.AddToRoleAsync(user, "KatMaliki");

                    var newOwner = new Owner
                    {
                        FullName   = fullName,
                        Email      = string.IsNullOrWhiteSpace(email) ? null : email,
                        Phone      = normalizedPhone,
                        TcKimlikNo = tcKimlik,
                        UserId     = user.Id,
                        IsActive   = true,
                        CreatedAt  = DateTime.Now
                    };
                    _db.Owners.Add(newOwner);
                    await _db.SaveChangesAsync();

                    row.Password = tcKimlik;
                    row.Phone    = normalizedPhone;
                    row.OwnerId  = newOwner.Id;
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
                result.Rows.Where(x => x.Success).Select(x => new { x.FullName, x.Phone, x.Password }));

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
            ws.Cell(1, 2).Value = "Telefon (kullanıcı adı)";
            ws.Cell(1, 3).Value = "Şifre (T.C. Kimlik No)";

            var header = ws.Range(1, 1, 1, 3);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F4FB");

            for (var i = 0; i < rows.Count; i++)
            {
                ws.Cell(i + 2, 1).Value = rows[i].FullName;
                ws.Cell(i + 2, 2).Value = rows[i].Phone;
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
            public string Phone    { get; set; } = "";
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

            // Bağımsız Bölüm Ata paneli için: sahipsiz bölümler + bu malike zaten ait olanlar
            // (başka bir malike ait olan bölümler burada listelenmez — o değişiklik bilerek Bağımsız
            // Bölümler ekranından yapılmalı, yanlışlıkla başka birinden bölüm çalınmasın).
            ViewBag.AssignableUnits = await _db.Units
                .Include(u => u.Project)
                .Where(u => u.OwnerId == null || u.OwnerId == id)
                .OrderBy(u => u.Project!.Name).ThenBy(u => u.UnitNo)
                .ToListAsync();

            return View(owner);
        }

        /// <summary>
        /// Bu malike hangi bağımsız bölümlerin ait olacağını tek ekrandan (Kat Maliki Düzenle) belirler.
        /// Sadece sahipsiz bölümler ve zaten bu malike ait olan bölümler seçilebilir kapsamdadır —
        /// başka bir malike ait bir bölüm, unitIds içine sızsa bile buradan çalınamaz.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignUnits(int ownerId, List<int>? unitIds)
        {
            var owner = await _db.Owners.FindAsync(ownerId);
            if (owner == null) return NotFound();

            unitIds ??= new List<int>();

            var assignableUnits = await _db.Units
                .Where(u => u.OwnerId == null || u.OwnerId == ownerId)
                .ToListAsync();

            foreach (var unit in assignableUnits)
            {
                var shouldOwn = unitIds.Contains(unit.Id);
                if (shouldOwn && unit.OwnerId != ownerId)
                {
                    unit.OwnerId = ownerId;
                    unit.UpdatedAt = DateTime.Now;
                }
                else if (!shouldOwn && unit.OwnerId == ownerId)
                {
                    unit.OwnerId = null;
                    unit.UpdatedAt = DateTime.Now;
                }
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Bağımsız bölüm ataması güncellendi.";
            return RedirectToAction(nameof(Edit), new { id = ownerId });
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

        /// <summary>
        /// Kat malikinin SMS/E-posta/WhatsApp bildirim kanallarını ve (varsa) telefon numarasını
        /// admin panelinden düzenlemeyi sağlar. Malikin daha önce (KatMaliki &gt; Profilim'den)
        /// hiç tercih kaydı oluşturmamış olma ihtimaline karşı find-or-create yapılır — WhatsApp
        /// dahil her kanal varsayılan olarak açık başlar.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNotificationPreferences(int ownerId, string? phone, bool smsEnabled, bool emailEnabled, bool whatsAppEnabled)
        {
            var owner = await _db.Owners.FindAsync(ownerId);
            if (owner == null) return NotFound();

            owner.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            owner.UpdatedAt = DateTime.Now;

            var pref = await _db.OwnerNotificationPreferences.FirstOrDefaultAsync(p => p.OwnerId == ownerId);
            if (pref == null)
            {
                pref = new OwnerNotificationPreference { OwnerId = ownerId, CreatedAt = DateTime.Now };
                _db.OwnerNotificationPreferences.Add(pref);
            }

            pref.SmsEnabled      = smsEnabled;
            pref.EmailEnabled    = emailEnabled;
            pref.WhatsAppEnabled = whatsAppEnabled;
            pref.UpdatedAt       = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"{owner.FullName} için bildirim tercihleri güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Telefon (giriş kullanıcı adı) veya T.C. Kimlik No (giriş şifresi) değiştiyse
        /// Identity hesabını da senkronize eder. Telefon değişmediyse ya da yeni değer geçersizse
        /// dokunulmaz (mevcut girişi bozmamak için sessizce eski değer korunur, admin'e uyarı gösterilir).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Owner owner)
        {
            var existing = await _db.Owners.FindAsync(id);
            if (existing == null) return NotFound();

            var user = await _users.FindByIdAsync(existing.UserId);
            var warnings = new List<string>();

            // Telefon (=UserName) değişti mi?
            var normalizedPhone = PhoneNormalizer.Normalize(owner.Phone);
            if (normalizedPhone == null)
            {
                warnings.Add("Telefon numarası geçersiz görünüyor, giriş bilgisi değiştirilmedi.");
            }
            else if (user != null && !string.Equals(user.UserName, normalizedPhone, StringComparison.OrdinalIgnoreCase))
            {
                var conflict = await _users.FindByNameAsync(normalizedPhone);
                if (conflict != null && conflict.Id != user.Id)
                {
                    warnings.Add("Bu telefon numarası başka bir hesapta kayıtlı, giriş bilgisi değiştirilmedi.");
                }
                else
                {
                    user.UserName = normalizedPhone;
                    user.NormalizedUserName = normalizedPhone.ToUpperInvariant();
                    await _users.UpdateAsync(user);
                }
            }
            existing.Phone = normalizedPhone ?? existing.Phone;

            // T.C. Kimlik No (=şifre) değişti mi?
            if (!string.IsNullOrWhiteSpace(owner.TcKimlikNo) && owner.TcKimlikNo != existing.TcKimlikNo)
            {
                if (!PhoneNormalizer.IsValidTcKimlikNo(owner.TcKimlikNo))
                {
                    warnings.Add("T.C. Kimlik No geçersiz (11 haneli olmalı), şifre değiştirilmedi.");
                }
                else if (user != null)
                {
                    var token = await _users.GeneratePasswordResetTokenAsync(user);
                    var resetResult = await _users.ResetPasswordAsync(user, token, owner.TcKimlikNo);
                    if (resetResult.Succeeded)
                        existing.TcKimlikNo = owner.TcKimlikNo;
                    else
                        warnings.Add("Şifre güncellenemedi: " + string.Join(" ", resetResult.Errors.Select(e => e.Description)));
                }
            }

            existing.FullName = owner.FullName;
            existing.Email    = owner.Email;
            existing.IsActive = owner.IsActive;
            existing.UpdatedAt = DateTime.Now;

            // Hesap pasife alınırsa girişini de kilitle
            if (user != null)
                await _users.SetLockoutEndDateAsync(user, existing.IsActive ? null : DateTimeOffset.MaxValue);

            await _db.SaveChangesAsync();

            TempData["Success"] = warnings.Count == 0
                ? "Kat maliki bilgileri güncellendi."
                : "Kat maliki bilgileri güncellendi. Uyarı: " + string.Join(" ", warnings);
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

        /// <summary>Malikin şifresini yeniden T.C. Kimlik No'suna sıfırlar (giriş şeması: telefon +
        /// TC No — bkz. KatMaliki/AccountController). TC No kayıtlı değilse önce Düzenle ekranından
        /// girilmesi gerekir, rastgele bir şifre üretilmez.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var owner = await _db.Owners.FindAsync(id);
            if (owner == null) return NotFound();

            if (!PhoneNormalizer.IsValidTcKimlikNo(owner.TcKimlikNo))
            {
                TempData["Error"] = "Bu malikin T.C. Kimlik No'su kayıtlı değil. Önce Düzenle ekranından girin, şifre otomatik olarak buna sıfırlanacaktır.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _users.FindByIdAsync(owner.UserId);
            if (user == null) return NotFound();

            var token  = await _users.GeneratePasswordResetTokenAsync(user);
            var result = await _users.ResetPasswordAsync(user, token, owner.TcKimlikNo!);

            if (result.Succeeded)
                TempData["Success"] = $"Şifre T.C. Kimlik No'suna sıfırlandı ({owner.FullName} — giriş: {user.UserName} / {owner.TcKimlikNo}).";
            else
                TempData["Error"] = "Şifre sıfırlanamadı: " + string.Join(" ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Kat malikini kalıcı olarak siler. Bağımsız bölümleri (Unit) silmez, sadece sahipsiz bırakır
        /// (Unit.OwnerId → null, veritabanı düzeyinde SetNull olarak yapılandırılmış) — böylece bölüm
        /// başka bir malike yeniden atanabilir. Bildirim kaydı/erişim/anket oyu gibi bu malike ait diğer
        /// kayıtlar veritabanı düzeyinde otomatik silinir (Cascade). Girişte kullandığı hesabı (AspNetUsers)
        /// da ayrıca siler — bu, Owner tablosuyla gerçek bir FK ilişkisi olmadığı için EF'in otomatik
        /// halledemeyeceği ayrı bir adımdır.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var owner = await _db.Owners.FindAsync(id);
            if (owner == null) return NotFound();

            var fullName = owner.FullName;
            var userId = owner.UserId;

            try
            {
                _db.Owners.Remove(owner);
                await _db.SaveChangesAsync();

                var user = await _users.FindByIdAsync(userId);
                if (user != null) await _users.DeleteAsync(user);

                TempData["Success"] = $"{fullName} kalıcı olarak silindi. Varsa bağımsız bölümleri sahipsiz (atanmamış) durumda kaldı.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Kat maliki silinemedi: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
