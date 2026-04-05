using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.Areas.Admin.Controllers
{
    [Area("Admin"), Authorize]
    public class MessagesController : Controller
    {
        private readonly ContactService _contact;
        public MessagesController(ContactService contact) => _contact = contact;

        public async Task<IActionResult> Index() => View(await _contact.GetAllAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id) { await _contact.MarkReadAsync(id); return RedirectToAction(nameof(Index)); }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id) { await _contact.DeleteAsync(id); TempData["Success"] = "Mesaj silindi."; return RedirectToAction(nameof(Index)); }
    }
}
