using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Models.ViewModels;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ProjectService _projects;
        private readonly SiteSettingService _settings;
        private readonly ReferenceService _refs;
        private readonly ContactService _contact;
        private readonly AppDbContext _db;

        public HomeController(ProjectService projects, SiteSettingService settings, ReferenceService refs, ContactService contact, AppDbContext db)
        {
            _projects = projects;
            _settings = settings;
            _refs     = refs;
            _contact  = contact;
            _db       = db;
        }

        public async Task<IActionResult> Index()
        {
            var allProjects = await _projects.GetAllActiveAsync();
            var vm = new HomeViewModel
            {
                HeroProjects      = await _projects.GetFeaturedAsync(),
                OngoingProjects   = allProjects.Where(p => p.Status == ProjectStatus.Ongoing).ToList(),
                CompletedProjects = allProjects.Where(p => p.Status == ProjectStatus.Completed).ToList(),
                AllProjects       = allProjects,
                Settings          = await _settings.GetAllAsync(),
                References        = await _refs.GetActiveAsync()
            };
            return View(vm);
        }

        [Route("Hakkimizda")]
        public async Task<IActionResult> Hakkimizda()
        {
            if (TempData["ContactSuccess"] != null) ViewBag.ContactSuccess = true;
            if (TempData["ContactError"]   != null) ViewBag.ContactError   = TempData["ContactError"];
            ViewBag.References = await _refs.GetActiveAsync();
            return View();
        }

        [HttpPost, Route("Hakkimizda"), ValidateAntiForgeryToken]
        public async Task<IActionResult> HakkimizdaContact(string fullName, string email, string? phone, string? subject, string message)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ContactError"] = "Lütfen zorunlu alanları doldurun.";
                return Redirect("/Hakkimizda#iletisim");
            }
            await _contact.SendAsync(new ContactMessage
            {
                FullName  = fullName.Trim(),
                Email     = email.Trim(),
                Phone     = phone?.Trim(),
                Subject   = subject?.Trim(),
                Message   = message.Trim(),
                CreatedAt = DateTime.Now
            });
            TempData["ContactSuccess"] = true;
            return Redirect("/Hakkimizda#iletisim");
        }

        [Route("Hizmetlerimiz")]
        public IActionResult Hizmetlerimiz() => View();

        [Route("ProjeHaritasi")]
        public async Task<IActionResult> ProjeHaritasi()
        {
            var ongoing   = await _projects.GetAllActiveAsync(ProjectStatus.Ongoing);
            var completed = await _projects.GetAllActiveAsync(ProjectStatus.Completed);
            return View(ongoing.Concat(completed).ToList());
        }

        [Route("Iletisim")]
        public async Task<IActionResult> Iletisim()
        {
            if (TempData["ContactSuccess"] != null) ViewBag.ContactSuccess = true;
            if (TempData["ContactError"]   != null) ViewBag.ContactError   = TempData["ContactError"];
            var settings = await _settings.GetAllAsync();
            ViewBag.Phone     = settings.GetValueOrDefault("PhoneNumber");
            ViewBag.Email     = settings.GetValueOrDefault("Email");
            ViewBag.Address   = settings.GetValueOrDefault("Address");
            ViewBag.WhatsApp  = settings.GetValueOrDefault("WhatsApp");
            ViewBag.Instagram = settings.GetValueOrDefault("Instagram");
            ViewBag.Facebook  = settings.GetValueOrDefault("Facebook");
            ViewBag.LinkedIn  = settings.GetValueOrDefault("LinkedIn");
            return View();
        }

        [HttpPost, Route("Iletisim"), ValidateAntiForgeryToken]
        public async Task<IActionResult> IletisimContact(string fullName, string email, string? phone, string? subject, string message)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ContactError"] = "Lütfen zorunlu alanları doldurun.";
                return RedirectToAction(nameof(Iletisim));
            }
            await _contact.SendAsync(new ContactMessage
            {
                FullName  = fullName.Trim(),
                Email     = email.Trim(),
                Phone     = phone?.Trim(),
                Subject   = subject?.Trim(),
                Message   = message.Trim(),
                CreatedAt = DateTime.Now
            });
            TempData["ContactSuccess"] = true;
            return RedirectToAction(nameof(Iletisim));
        }

        /// <summary>Herkese açık Sıkça Sorulan Sorular sayfası. İçerik, admin panelindeki
        /// "Sıkça Sorulan Sorular" kayıtlarından gelir (Kat Maliki portalıyla aynı liste).</summary>
        [Route("SSS")]
        public async Task<IActionResult> Sss()
        {
            var items = await _db.Faqs
                .Where(f => f.IsActive)
                .OrderBy(f => f.OrderIndex).ThenBy(f => f.Id)
                .ToListAsync();
            return View(items);
        }

        [Route("Sertifikalar")]
        public async Task<IActionResult> Sertifikalar()
        {
            var certs = await _db.Certificates
                .Where(c => c.IsActive)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();
            return View(certs);
        }

        [Route("E-Katalog")]
        public async Task<IActionResult> ECatalog()
        {
            var catalog = await _db.Catalogs.FirstOrDefaultAsync(c => c.IsActive);
            if (catalog != null && !string.IsNullOrEmpty(catalog.PdfPath))
                return Redirect(catalog.PdfPath);
            TempData["CatalogError"] = "Şu an aktif bir katalog bulunmamaktadır.";
            return RedirectToAction(nameof(Hakkimizda));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
