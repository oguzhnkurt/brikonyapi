using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signIn;
        private readonly UserManager<IdentityUser>   _users;

        public AccountController(SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users)
        {
            _signIn = signIn;
            _users  = users;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signIn.IsSignedIn(User)) return RedirectToAction("Index", "Dashboard");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            var result = await _signIn.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: false);
            if (result.Succeeded)
                return LocalRedirect(returnUrl ?? "/Admin/Dashboard");

            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Error = "E-posta veya şifre hatalı.";
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction("Login");
        }

        [HttpGet, Authorize]
        public IActionResult ChangePassword()
        {
            ViewData["Title"] = "Şifre Değiştir";
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            ViewData["Title"] = "Şifre Değiştir";
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Yeni şifre ve onay şifresi eşleşmiyor.";
                return View();
            }
            var user = await _users.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                await _signIn.RefreshSignInAsync(user);
                TempData["Success"] = "Şifreniz başarıyla güncellendi.";
                return RedirectToAction("ChangePassword");
            }
            TempData["Error"] = "Mevcut şifre hatalı veya yeni şifre gereksinimlerini karşılamıyor (min. 8 karakter, büyük harf ve rakam içermeli).";
            return View();
        }
    }
}
