using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize]
    public class DashboardController : Controller
    {
        private readonly ProjectService _projects;
        private readonly ContactService _contact;

        public DashboardController(ProjectService projects, ContactService contact)
        {
            _projects = projects;
            _contact  = contact;
        }

        public async Task<IActionResult> Index()
        {
            var all = await _projects.GetAllAsync();
            ViewBag.OngoingCount   = all.Count(p => p.Status == ProjectStatus.Ongoing);
            ViewBag.CompletedCount = all.Count(p => p.Status == ProjectStatus.Completed);
            ViewBag.TotalCount     = all.Count;
            ViewBag.UnreadCount    = await _contact.GetUnreadCountAsync();
            return View();
        }
    }
}
