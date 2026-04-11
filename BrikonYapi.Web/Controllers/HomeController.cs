using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Models.ViewModels;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ProjectService _projects;
        private readonly SiteSettingService _settings;
        private readonly ReferenceService _refs;
        private readonly ContactService _contact;

        public HomeController(ProjectService projects, SiteSettingService settings, ReferenceService refs, ContactService contact)
        {
            _projects = projects;
            _settings = settings;
            _refs     = refs;
            _contact  = contact;
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
