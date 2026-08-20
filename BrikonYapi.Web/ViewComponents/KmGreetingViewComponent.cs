using BrikonYapi.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.ViewComponents
{
    /// <summary>
    /// Kat Maliki Portalı üst başlığındaki kişisel karşılama: "Hoş geldiniz, {Ad Soyad}" + rumuz rozeti.
    /// Giriş yapan kullanıcının Owner kaydı yoksa (ör. Admin önizlemesi) sessizce "Kat Maliki" gösterir.
    /// </summary>
    public class KmGreetingViewComponent : ViewComponent
    {
        private readonly AppDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public KmGreetingViewComponent(AppDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var fullName = "Kat Maliki";

            var userId = _users.GetUserId(UserClaimsPrincipal);
            if (!string.IsNullOrEmpty(userId))
            {
                var name = await _db.Owners
                    .Where(o => o.UserId == userId)
                    .Select(o => o.FullName)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(name)) fullName = name;
            }

            // Rozet için ilk harf (ör. "Altuğ Alver" -> "A")
            var initial = fullName.Trim().Length > 0 ? fullName.Trim()[0].ToString().ToUpperInvariant() : "K";

            return View(new KmGreetingModel { FullName = fullName, Initial = initial });
        }
    }

    public class KmGreetingModel
    {
        public string FullName { get; set; } = "Kat Maliki";
        public string Initial { get; set; } = "K";
    }
}
