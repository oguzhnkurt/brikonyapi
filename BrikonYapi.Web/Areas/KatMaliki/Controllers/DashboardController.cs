using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Areas.KatMaliki.Controllers
{
    [Area("KatMaliki"), Authorize(Roles = "KatMaliki")]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<IdentityUser> _users;
        private readonly IWebHostEnvironment _env;
        private readonly PaymentNotificationService _notify;

        public DashboardController(AppDbContext db, UserManager<IdentityUser> users, IWebHostEnvironment env, PaymentNotificationService notify)
        {
            _db    = db;
            _users = users;
            _env   = env;
            _notify = notify;
        }

        private async Task<Owner?> GetCurrentOwnerAsync()
        {
            var userId = _users.GetUserId(User);
            if (userId == null) return null;
            return await _db.Owners
                .Include(o => o.Units).ThenInclude(u => u.Project)
                .Include(o => o.Units).ThenInclude(u => u.PaymentSchedules).ThenInclude(p => p.Transactions)
                .Include(o => o.Units).ThenInclude(u => u.PaymentSchedules).ThenInclude(p => p.ProjectStage)
                .FirstOrDefaultAsync(o => o.UserId == userId);
        }

        public async Task<IActionResult> Index()
        {
            var owner = await GetCurrentOwnerAsync();
            if (owner == null) return NotFound("Bu kullanıcıya bağlı bir kat maliki profili bulunamadı.");
            return View(owner);
        }

        /// <summary>Tek bir taksidin detayı.
        /// GÜVENLİK: taksit, giriş yapan malikin bağımsız bölümlerinden birine ait değilse 404 döner (IDOR koruması).</summary>
        public async Task<IActionResult> Detail(int id, [FromServices] CardPaymentService cards)
        {
            var owner = await GetCurrentOwnerAsync();
            if (owner == null) return NotFound();

            var schedule = owner.Units
                .SelectMany(u => u.PaymentSchedules)
                .FirstOrDefault(s => s.Id == id);

            if (schedule == null) return NotFound();

            // Sonucu henüz bilinmeyen kart denemesi varsa (ör. malik TKPAY sayfasından dönmeden
            // tarayıcıyı kapattıysa) sayfa açılırken TKPAY'den doğrula.
            if (cards.IsAvailable)
            {
                foreach (var open in schedule.Transactions.Where(t => t.Method == PaymentMethod.CreditCard
                                                                      && t.Status == PaymentTransactionStatus.Initiated).ToList())
                {
                    try { await cards.VerifyAsync(open, HttpContext.RequestAborted); }
                    catch (Exception) { /* sayfa yine açılır; arka plan işi tekrar dener */ }
                }
            }

            ViewBag.Unit = owner.Units.First(u => u.PaymentSchedules.Any(s => s.Id == id));
            ViewBag.CardAvailable = cards.IsAvailable && CardPaymentService.SupportsCurrency(schedule.Currency);
            ViewBag.CardConfigured = cards.IsAvailable;
            return View(schedule);
        }

        /// <summary>Kredi kartı ile ödeme: TKPAY güvenli ödeme sayfasına yönlendirir.
        /// Kart bilgisi bu siteye hiç girilmez. GÜVENLİK: taksit yalnızca giriş yapan malike aitse işlenir.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PayByCard(int scheduleId, bool acceptTerms,
            [FromServices] CardPaymentService cards)
        {
            var owner = await GetCurrentOwnerAsync();
            if (owner == null) return NotFound();

            var schedule = owner.Units.SelectMany(u => u.PaymentSchedules).FirstOrDefault(p => p.Id == scheduleId);
            if (schedule == null) return NotFound();

            if (!acceptTerms)
            {
                TempData["Error"] = "Devam etmek için Mesafeli Hizmet ve Ödeme Sözleşmesi ile Ön Bilgilendirme Formu'nu onaylayın.";
                return RedirectToAction(nameof(Detail), new { id = scheduleId });
            }

            var (url, error) = await cards.StartAsync(owner, schedule, HttpContext.RequestAborted);
            if (url == null)
            {
                TempData["Error"] = error ?? "Ödeme başlatılamadı.";
                return RedirectToAction(nameof(Detail), new { id = scheduleId });
            }
            return Redirect(url); // TkpayClient yalnızca *.tkpay.com https adreslerine izin verir
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportBankTransfer(int scheduleId, IFormFile receipt)
        {
            var owner = await GetCurrentOwnerAsync();
            if (owner == null) return NotFound();

            var schedule = owner.Units.SelectMany(u => u.PaymentSchedules).FirstOrDefault(p => p.Id == scheduleId);
            if (schedule == null)
            {
                TempData["Error"] = "Ödeme kalemi bulunamadı.";
                return RedirectToAction(nameof(Detail), new { id = scheduleId });
            }

            if (receipt == null || receipt.Length == 0)
            {
                TempData["Error"] = "Lütfen dekont dosyası yükleyin.";
                return RedirectToAction(nameof(Detail), new { id = scheduleId });
            }

            if (receipt.Length > 10 * 1024 * 1024)
            {
                TempData["Error"] = "Dekont dosyası 10 MB'ı aşamaz.";
                return RedirectToAction(nameof(Detail), new { id = scheduleId });
            }

            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".webp" };
            var ext = Path.GetExtension(receipt.FileName).ToLowerInvariant();
            if (!allowedExt.Contains(ext))
            {
                TempData["Error"] = "Sadece JPG, PNG, WEBP veya PDF dosyası yükleyebilirsiniz.";
                return RedirectToAction(nameof(Detail), new { id = scheduleId });
            }

            var dir = Path.Combine(_env.WebRootPath, "uploads", "receipts");
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid()}{ext}";
            await using (var fs = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
                await receipt.CopyToAsync(fs);

            _db.PaymentTransactions.Add(new PaymentTransaction
            {
                PaymentScheduleId = schedule.Id,
                Method            = PaymentMethod.BankTransfer,
                Amount            = schedule.Amount,
                ReceiptFilePath   = $"/uploads/receipts/{fileName}",
                Status            = PaymentTransactionStatus.PendingApproval,
                CreatedAt         = DateTime.Now
            });
            await _db.SaveChangesAsync();

            await _notify.NotifyAdminReceiptUploadedAsync(owner, schedule);

            TempData["Success"] = "Havale bildiriminiz alındı, en kısa sürede onaylanacaktır.";
            return RedirectToAction(nameof(Detail), new { id = scheduleId });
        }
    }
}
