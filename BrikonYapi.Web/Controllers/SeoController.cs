using System.Text;
using System.Xml.Linq;
using BrikonYapi.Web.Data;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Controllers
{
    /// <summary>Arama motorları için sitemap.xml ve robots.txt üretir.</summary>
    public class SeoController : Controller
    {
        private readonly AppDbContext _db;
        private readonly SiteSettingService _settings;

        public SeoController(AppDbContext db, SiteSettingService settings)
        {
            _db = db;
            _settings = settings;
        }

        private async Task<string> GetBaseUrlAsync()
        {
            var configured = (await _settings.GetAsync("SeoBaseUrl"))?.Trim();
            var baseUrl = !string.IsNullOrWhiteSpace(configured)
                ? configured!
                : $"{Request.Scheme}://{Request.Host}";
            return baseUrl.TrimEnd('/');
        }

        [ResponseCache(Duration = 3600)]
        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var baseUrl = await GetBaseUrlAsync();
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            XElement Url(string loc, DateTime? lastMod, string changeFreq, string priority)
            {
                var el = new XElement(ns + "url", new XElement(ns + "loc", baseUrl + loc));
                if (lastMod.HasValue)
                    el.Add(new XElement(ns + "lastmod", lastMod.Value.ToString("yyyy-MM-dd")));
                el.Add(new XElement(ns + "changefreq", changeFreq));
                el.Add(new XElement(ns + "priority", priority));
                return el;
            }

            var root = new XElement(ns + "urlset");

            // Sabit sayfalar
            root.Add(Url("/", null, "weekly", "1.0"));
            root.Add(Url("/Projeler", null, "weekly", "0.9"));
            root.Add(Url("/Home/Hakkimizda", null, "monthly", "0.7"));
            root.Add(Url("/Home/Iletisim", null, "monthly", "0.7"));
            root.Add(Url("/Home/Sertifikalar", null, "monthly", "0.5"));

            // Proje detay sayfaları
            var projects = await _db.Projects
                .Where(p => p.IsActive)
                .Select(p => new { p.Slug, p.UpdatedAt, p.CreatedAt })
                .ToListAsync();

            foreach (var p in projects)
            {
                if (string.IsNullOrWhiteSpace(p.Slug)) continue;
                root.Add(Url($"/Projeler/{Uri.EscapeDataString(p.Slug)}", p.UpdatedAt ?? p.CreatedAt, "monthly", "0.8"));
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            return Content(doc.Declaration + Environment.NewLine + doc.ToString(), "application/xml", Encoding.UTF8);
        }

        [ResponseCache(Duration = 3600)]
        [Route("robots.txt")]
        public async Task<IActionResult> Robots()
        {
            var baseUrl = await GetBaseUrlAsync();
            var noIndex = string.Equals((await _settings.GetAsync("SeoNoIndex"))?.Trim(), "true",
                                        StringComparison.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");

            if (noIndex)
            {
                // Test/hazırlık ortamı: tüm site aramaya kapalı.
                sb.AppendLine("Disallow: /");
            }
            else
            {
                // Yönetim ve kat maliki portalı arama sonuçlarında yer almamalı.
                sb.AppendLine("Disallow: /Admin");
                sb.AppendLine("Disallow: /KatMaliki");
                sb.AppendLine("Disallow: /uploads/receipts");
                sb.AppendLine("Allow: /");
                sb.AppendLine();
                sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
            }

            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }
    }
}
