using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BrikonYapi.Web.Areas.KatMaliki.Controllers
{
    [Area("KatMaliki")]
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signIn;
        private readonly UserManager<IdentityUser>   _users;
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public AccountController(SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users, AppDbContext db, IConfiguration config)
        {
            _signIn = signIn;
            _users  = users;
            _db     = db;
            _config = config;
        }

        private bool GoogleEnabled => !string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientId"]);

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signIn.IsSignedIn(User) && User.IsInRole("KatMaliki"))
                return RedirectToAction("Index", "Progress");

            ViewBag.ReturnUrl = returnUrl;
            ViewBag.GoogleEnabled = GoogleEnabled;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            var result = await _signIn.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                var user = await _users.FindByEmailAsync(email);
                if (user != null && await _users.IsInRoleAsync(user, "KatMaliki"))
                    return LocalRedirect(returnUrl ?? "/KatMaliki/Progress");

                await _signIn.SignOutAsync();
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.GoogleEnabled = GoogleEnabled;
                ViewBag.Error = "Bu hesabın kat maliki paneline erişim yetkisi yok.";
                return View();
            }

            if (result.IsLockedOut)
                ViewBag.Error = "Hesabınız pasif durumda. Lütfen yönetici ile iletişime geçin.";
            else
                ViewBag.Error = "E-posta veya şifre hatalı.";

            ViewBag.ReturnUrl = returnUrl;
            ViewBag.GoogleEnabled = GoogleEnabled;
            return View();
        }

        // ── Google ile Giriş / Kayıt ──────────────────────────────
        // İlk girişte otomatik hesap + Kat Maliki profili oluşturur (ad/e-posta
        // Google hesabından alınır). Bağımsız bölüm ataması hâlâ admin tarafından
        // sonradan yapılır.

        [HttpGet]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(GoogleCallback), "Account", new { area = "KatMaliki", returnUrl });
            var properties = _signIn.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        private IActionResult GoogleLoginFailed(string message, string? returnUrl)
        {
            ViewBag.Error = message;
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.GoogleEnabled = GoogleEnabled;
            return View("Login");
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
        {
            var info = await _signIn.GetExternalLoginInfoAsync();
            if (info == null)
                return GoogleLoginFailed("Google ile giriş başarısız oldu. Lütfen tekrar deneyin.", returnUrl);

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var name  = info.Principal.FindFirstValue(ClaimTypes.Name)
                        ?? $"{info.Principal.FindFirstValue(ClaimTypes.GivenName)} {info.Principal.FindFirstValue(ClaimTypes.Surname)}".Trim();

            if (string.IsNullOrWhiteSpace(email))
                return GoogleLoginFailed("Google hesabınızdan e-posta bilgisi alınamadı.", returnUrl);

            // 1) Bu Google hesabı daha önce baglanmis mi?
            var signInResult = await _signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
            if (signInResult.Succeeded)
            {
                var existingLinkedUser = await _users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (existingLinkedUser != null && !await _users.IsInRoleAsync(existingLinkedUser, "KatMaliki"))
                {
                    await _signIn.SignOutAsync();
                    return GoogleLoginFailed("Bu Google hesabının kat maliki paneline erişim yetkisi yok.", returnUrl);
                }
                return LocalRedirect(returnUrl ?? "/KatMaliki/Progress");
            }

            if (signInResult.IsLockedOut)
                return GoogleLoginFailed("Hesabınız pasif durumda. Lütfen yönetici ile iletişime geçin.", returnUrl);

            // 2) Google hesabi baglanmamis - e-postaya gore mevcut kullanici var mi?
            var user = await _users.FindByEmailAsync(email);
            var isNewUser = user == null;

            if (user == null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var createResult = await _users.CreateAsync(user);
                if (!createResult.Succeeded)
                    return GoogleLoginFailed("Hesap oluşturulamadı: " + string.Join(" ", createResult.Errors.Select(e => e.Description)), returnUrl);
            }
            else if (!await _users.IsInRoleAsync(user, "KatMaliki"))
            {
                // Bu e-posta baska bir rolde (orn. Admin) kayitli - Kat Maliki girisine izin verme
                return GoogleLoginFailed("Bu e-posta adresi kat maliki paneline erişim için uygun değil.", returnUrl);
            }

            if (!await _users.IsInRoleAsync(user, "KatMaliki"))
                await _users.AddToRoleAsync(user, "KatMaliki");

            if (isNewUser)
            {
                _db.Owners.Add(new Owner
                {
                    UserId    = user.Id,
                    FullName  = string.IsNullOrWhiteSpace(name) ? email : name,
                    Email     = email,
                    IsActive  = true,
                    CreatedAt = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }

            var addLoginResult = await _users.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
                return GoogleLoginFailed("Google hesabı bağlanamadı: " + string.Join(" ", addLoginResult.Errors.Select(e => e.Description)), returnUrl);

            await _signIn.SignInAsync(user, isPersistent: true);
            return LocalRedirect(returnUrl ?? "/KatMaliki/Progress");
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "KatMaliki")]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}
