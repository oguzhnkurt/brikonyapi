using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Models.ViewModels;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Controllers
{
    public class ProjectsController : BaseController
    {
        private readonly ProjectService _projects;
        private readonly AppDbContext _db;
        private readonly SiteSettingService _settings;

        public ProjectsController(ProjectService projects, AppDbContext db, SiteSettingService settings)
        {
            _projects = projects;
            _db = db;
            _settings = settings;
        }

        public async Task<IActionResult> Index(string tab = "all")
        {
            var ongoing   = await _projects.GetAllActiveAsync(ProjectStatus.Ongoing);
            var completed = await _projects.GetAllActiveAsync(ProjectStatus.Completed);

            var list = tab switch
            {
                "ongoing"   => ongoing,
                "completed" => completed,
                _           => await _projects.GetAllActiveAsync()
            };

            ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.OrderIndex).ThenBy(c => c.Name).ToListAsync();

            return View(new ProjectListViewModel
            {
                Projects       = list,
                ActiveTab      = tab,
                OngoingCount   = ongoing.Count,
                CompletedCount = completed.Count
            });
        }

        /// <summary>Proje detay sayfası. SEO açısından en değerli sayfalar bunlardır;
        /// her proje kendi başlığı, açıklaması ve görseliyle indekslenir.</summary>
        public async Task<IActionResult> Detail(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return RedirectToAction(nameof(Index));

            var project = await _projects.GetBySlugAsync(slug);

            // Yayında olmayan / bulunamayan projede listeye yönlendirmek yerine 404 döneriz;
            // böylece arama motorları ölü bağlantıyı doğru şekilde işler.
            if (project == null)
                return NotFound();

            ViewBag.Categories = await _db.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.OrderIndex).ThenBy(c => c.Name)
                .ToListAsync();

            // Sayfadaki "Bilgi Al" bağlantısı için iletişim telefonu
            ViewBag.Phone = await _settings.GetAsync("PhoneNumber");

            // Aynı kategoriden diğer projeler (sayfa altı öneriler)
            ViewBag.RelatedProjects = await _db.Projects
                .Where(p => p.IsActive && p.Id != project.Id
                            && (project.CategoryId == null || p.CategoryId == project.CategoryId))
                .OrderByDescending(p => p.IsFeatured)
                .ThenBy(p => p.OrderIndex)
                .Take(3)
                .ToListAsync();

            return View(project);
        }
    }
}
