using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>
    /// Panelin kendisine tam yetkili yönetici hesabı ekleyip yönetmeyi sağlar (kat malikleri —
    /// OwnersController — ile karıştırılmamalı, bu tamamen ayrı bir akış).
    /// NOT: Sistemde şu an "Admin" rolünden daha kısıtlı bir yönetici rolü yok — bu ekrandan
    /// eklenen her hesap, ödemeler ve kat malikleri kişisel bilgileri dahil panelin tamamına
    /// erişebilir. Sadece güvenilir kişiler/kurumlar için kullanılmalı.
    /// </summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<IdentityUser> _users;

        public AdminUsersController(UserManager<IdentityUser> users)
        {
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var admins = await _users.GetUsersInRoleAsync("Admin");
            return View(admins.OrderBy(u => u.Email).ToList());
        }

        public IActionResult Create() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "E-posta ve şifre zorunludur.";
                return RedirectToAction(nameof(Create));
            }

            var existing = await _users.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["Error"] = "Bu e-posta adresiyle zaten bir kullanıcı kayıtlı.";
                return RedirectToAction(nameof(Create));
            }

            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var createResult = await _users.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                TempData["Error"] = string.Join(" ", createResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Create));
            }

            await _users.AddToRoleAsync(user, "Admin");

            TempData["Success"] = $"Yönetici hesabı oluşturuldu. Giriş bilgileri — E-posta: {email} · Şifre: {password} (bu şifreyi güvenli bir şekilde iletin, tekrar gösterilmeyecektir).";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Hesabı devre dışı bırakır (silmez) — erişimi geri açmak için ResetPassword ile birlikte kilit kaldırılabilir.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _users.SetLockoutEnabledAsync(user, true);
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            TempData["Success"] = "Yönetici hesabı devre dışı bırakıldı.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _users.SetLockoutEndDateAsync(user, null);
            TempData["Success"] = "Yönetici hesabı yeniden etkinleştirildi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound();

            var newPassword = OwnerPasswordGenerator.Generate();
            var token  = await _users.GeneratePasswordResetTokenAsync(user);
            var result = await _users.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
                TempData["Success"] = $"Yeni şifre: {newPassword} (bu şifreyi güvenli bir şekilde iletin, tekrar gösterilmeyecektir).";
            else
                TempData["Error"] = "Şifre sıfırlanamadı: " + string.Join(" ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }
    }
}
