using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize]
    public class SettingsController : Controller
    {
        private readonly SiteSettingService _settings;
        public SettingsController(SiteSettingService settings) => _settings = settings;

        public async Task<IActionResult> Index() => View(await _settings.GetAllAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Dictionary<string, string> settings)
        {
            await _settings.SaveAllAsync(settings);
            TempData["Success"] = "Ayarlar kaydedildi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
