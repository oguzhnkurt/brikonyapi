using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Controllers
{
    /// <summary>
    /// TKPAY ödeme sayfasından dönüş (callbackOkUrl / callbackFailUrl).
    ///
    /// TKPAY malikin tarayıcısını bu adrese POST ile yönlendirir. Bu istek başka bir siteden geldiği için
    /// oturum çerezi (SameSite=Lax) gelmez — bu yüzden uç nokta anonimdir ve antiforgery doğrulaması
    /// yapılmaz. GÜVENLİK: gelen form verisine (status/amount vb.) HİÇ güvenilmez; yalnızca externalId
    /// alınır ve sonuç sunucudan sunucuya TKPAY sorgusuyla (CardPaymentService.VerifyAsync) belirlenir.
    /// Ardından malik kendi taksit sayfasına (GET) yönlendirilir; orada oturumu normal şekilde açıktır.
    /// </summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class TkpayReturnController : Controller
    {
        private readonly AppDbContext _db;
        private readonly CardPaymentService _cards;

        public TkpayReturnController(AppDbContext db, CardPaymentService cards)
        {
            _db = db;
            _cards = cards;
        }

        [HttpPost, HttpGet]
        [Route("odeme/tkpay/donus")]
        public async Task<IActionResult> Return()
        {
            string? externalId = null;
            if (Request.HasFormContentType) externalId = Request.Form["externalId"].FirstOrDefault();
            externalId ??= Request.Query["externalId"].FirstOrDefault();
            externalId = externalId?.Trim();

            if (string.IsNullOrEmpty(externalId) || externalId.Length > 20)
                return Redirect("/KatMaliki/Dashboard");

            var tx = await _db.PaymentTransactions
                .FirstOrDefaultAsync(t => t.ExternalId == externalId && t.Method == PaymentMethod.CreditCard);
            if (tx == null) return Redirect("/KatMaliki/Dashboard");

            CardPaymentOutcome outcome;
            try { outcome = await _cards.VerifyAsync(tx, HttpContext.RequestAborted); }
            catch (Exception) { outcome = CardPaymentOutcome.Pending; }

            var flag = outcome switch
            {
                CardPaymentOutcome.Completed   => "basarili",
                CardPaymentOutcome.Failed      => "basarisiz",
                CardPaymentOutcome.NeedsReview => "inceleme",
                _                              => "bekliyor"
            };
            return Redirect($"/KatMaliki/Dashboard/Detail/{tx.PaymentScheduleId}?kart={flag}");
        }
    }
}
