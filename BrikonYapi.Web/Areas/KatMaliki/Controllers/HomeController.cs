using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.Areas.KatMaliki.Controllers
{
    /// <summary>
    /// Eski "Ana Sayfa" panosu (karşılama + duyurular + projeler) İlerleme sayfasıyla
    /// birleştirildi. Bu controller sadece geriye dönük uyumluluk için (eski bağlantılar,
    /// yer imleri) /KatMaliki/Home adresini /KatMaliki/Progress'e yönlendirir.
    /// </summary>
    [Area("KatMaliki"), Authorize(Roles = "KatMaliki")]
    public class HomeController : Controller
    {
        public IActionResult Index() => RedirectToAction("Index", "Progress");
    }
}
