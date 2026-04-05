using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class CategoriesController : Controller
    {
        private readonly AppDbContext _db;
        public CategoriesController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var cats = await _db.Categories.OrderBy(c => c.OrderIndex).ThenBy(c => c.Name).ToListAsync();
            return View(cats);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string name, int orderIndex)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _db.Categories.Add(new Category { Name = name.Trim(), OrderIndex = orderIndex });
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string name, int orderIndex)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat != null && !string.IsNullOrWhiteSpace(name))
            {
                cat.Name = name.Trim();
                cat.OrderIndex = orderIndex;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat != null)
            {
                cat.IsActive = !cat.IsActive;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat != null)
            {
                _db.Categories.Remove(cat);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }
    }
}
