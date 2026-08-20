using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BrikonYapi.Web.ViewComponents
{
    /// <summary>
    /// Tüm genel sayfaların &lt;head&gt; bölümüne SEO etiketlerini basar:
    /// başlık, açıklama, anahtar kelimeler, canonical, Open Graph, Twitter Card ve JSON-LD.
    /// Varsayılan değerler Site Ayarları'ndan okunur; sayfalar ViewData ile bunları ezebilir.
    /// </summary>
    public class SeoMetaViewComponent : ViewComponent
    {
        private readonly SiteSettingService _settings;

        public SeoMetaViewComponent(SiteSettingService settings) => _settings = settings;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var s = await _settings.GetAllAsync();

            string Get(string key, string fallback = "")
            {
                return s.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v!.Trim() : fallback;
            }

            var siteName = Get("SeoSiteName", "Brikon Yapı");

            // Canonical taban adresi: ayarlarda tanımlıysa o kullanılır (ters vekil arkasında Host yanlış olabilir).
            var configuredBase = Get("SeoBaseUrl");
            var requestBase = $"{Request.Scheme}://{Request.Host}";
            var baseUrl = (configuredBase.Length > 0 ? configuredBase : requestBase).TrimEnd('/');

            var pageTitle = (ViewData["Title"] ?? "").ToString()!.Trim();
            var fullTitle = pageTitle.Length > 0 ? $"{pageTitle} - {siteName}" : Get("SeoDefaultTitle", siteName);

            var description = (ViewData["Description"] ?? "").ToString()!.Trim();
            if (description.Length == 0)
                description = Get("SeoDescription", "Brikon Yapı - Kaliteli ve güvenilir inşaat hizmetleriyle geleceği inşa ediyoruz.");

            var image = (ViewData["OgImage"] ?? "").ToString()!.Trim();
            if (image.Length == 0) image = Get("SeoOgImage", "/images/og-default.jpg");

            var model = new SeoMetaModel
            {
                SiteName = siteName,
                Title = fullTitle,
                Description = Truncate(description, 300),
                Keywords = Get("SeoKeywords"),
                // Open Graph mutlak adres ister; göreli yol verilirse tam adrese çevrilir.
                ImageUrl = ToAbsolute(image, baseUrl),
                CanonicalUrl = baseUrl + Request.Path,
                TwitterSite = Get("SeoTwitterSite"),
                GoogleVerification = Get("SeoGoogleVerification"),
                NoIndex = string.Equals(Get("SeoNoIndex"), "true", StringComparison.OrdinalIgnoreCase),
                OgType = (ViewData["OgType"] ?? "website").ToString()!,
                Locale = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en_US" : "tr_TR",

                BaseUrl = baseUrl
            };

            // SEO bölümünde ayrıca girilmişse önce onlar kullanılır; boşsa İletişim sekmesindeki genel hesaplara düşer.
            var facebook  = Get("SeoFacebook", Get("Facebook"));
            var instagram = Get("SeoInstagram", Get("Instagram"));

            model.JsonLd = BuildOrganizationJsonLd(
                model.SiteName, baseUrl,
                Get("PhoneNumber"), Get("Email"), Get("Address"),
                new[] { instagram, facebook, Get("LinkedIn") }.Where(x => x.Length > 0).Distinct().ToArray());

            return View(model);
        }

        /// <summary>Arama motorlarına kurum bilgisi (Google bilgi kartı) için schema.org JSON-LD üretir.</summary>
        private static string BuildOrganizationJsonLd(
            string siteName, string baseUrl, string phone, string email, string address, string[] social)
        {
            // Razor'da "@" kaçış karmaşası yaşamamak için anahtar adları burada üretilir.
            const string at = "@";

            var org = new Dictionary<string, object?>
            {
                [at + "context"] = "https://schema.org",
                [at + "type"] = "Organization",
                ["name"] = siteName,
                ["url"] = baseUrl,
                ["logo"] = baseUrl + "/images/logo.png"
            };

            if (phone.Length > 0 || email.Length > 0)
            {
                var contact = new Dictionary<string, object?>
                {
                    [at + "type"] = "ContactPoint",
                    ["contactType"] = "customer service",
                    ["areaServed"] = "TR",
                    ["availableLanguage"] = new[] { "Turkish", "English" }
                };
                if (phone.Length > 0) contact["telephone"] = phone;
                if (email.Length > 0) contact["email"] = email;
                org["contactPoint"] = contact;
            }

            if (address.Length > 0)
            {
                org["address"] = new Dictionary<string, object?>
                {
                    [at + "type"] = "PostalAddress",
                    ["streetAddress"] = address,
                    ["addressCountry"] = "TR"
                };
            }

            if (social.Length > 0) org["sameAs"] = social;

            return System.Text.Json.JsonSerializer.Serialize(org, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        private static string Truncate(string text, int max) =>
            text.Length <= max ? text : text.Substring(0, max - 1).TrimEnd() + "…";

        private static string ToAbsolute(string path, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return path;
            return baseUrl + "/" + path.TrimStart('/');
        }
    }

    public class SeoMetaModel
    {
        public string SiteName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Keywords { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string CanonicalUrl { get; set; } = "";
        public string TwitterSite { get; set; } = "";
        public string GoogleVerification { get; set; } = "";
        public bool NoIndex { get; set; }
        public string OgType { get; set; } = "website";
        public string Locale { get; set; } = "tr_TR";

        public string BaseUrl { get; set; } = "";

        /// <summary>Hazır schema.org JSON-LD çıktısı.</summary>
        public string JsonLd { get; set; } = "";
    }
}
