using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    /// <summary>Kat Maliki Portalı "Sıkça Sorulan Sorular" bölümünün yönetimi.</summary>
    [Area("Admin"), Authorize(Roles = "Admin")]
    public class FaqController : Controller
    {
        private readonly AppDbContext _db;

        public FaqController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _db.Faqs.OrderBy(f => f.OrderIndex).ThenByDescending(f => f.CreatedAt).ToListAsync();
            return View(list);
        }

        public IActionResult Create() => View(new FaqItem());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FaqItem faq)
        {
            if (!ModelState.IsValid) return View(faq);

            faq.CreatedAt = DateTime.Now;
            _db.Faqs.Add(faq);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Soru eklendi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await _db.Faqs.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FaqItem faq)
        {
            var existing = await _db.Faqs.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Question = faq.Question;
            existing.Answer = faq.Answer;
            existing.QuestionEn = faq.QuestionEn;
            existing.AnswerEn = faq.AnswerEn;
            existing.IsActive = faq.IsActive;
            existing.OrderIndex = faq.OrderIndex;
            existing.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Soru güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.Faqs.FindAsync(id);
            if (item != null)
            {
                _db.Faqs.Remove(item);
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Soru silindi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
