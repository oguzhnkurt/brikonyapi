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
        public ProjectsController(ProjectService projects, AppDbContext db) { _projects = projects; _db = db; }

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

        public IActionResult Detail(string slug)
        {
            return RedirectToAction("Index");
        }
    }
}
