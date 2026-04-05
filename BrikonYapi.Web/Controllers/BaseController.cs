using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BrikonYapi.Web.Controllers
{
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            ViewBag.IsEn = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
            base.OnActionExecuting(context);
        }
    }
}
