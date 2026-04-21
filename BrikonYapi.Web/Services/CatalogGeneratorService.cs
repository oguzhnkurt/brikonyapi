using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BrikonYapi.Web.Services
{
    public class CatalogGeneratorService
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        // Marka renkleri
        private static readonly string PrimaryHex = "#0082B4";
        private static readonly string DarkHex    = "#0A1628";
        private static readonly string GrayHex    = "#6B7280";

        public CatalogGeneratorService(AppDbContext db, IWebHostEnvironment env)
        {
            _db  = db;
            _env = env;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string> GenerateAsync()
        {
            var projects = await _db.Projects
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .OrderBy(p => p.OrderIndex)
                .ToListAsync();

            var settings = await _db.SiteSettings.ToListAsync();
            string Get(string key) => settings.FirstOrDefault(s => s.Key == key)?.Value ?? "";

            var dir = Path.Combine(_env.WebRootPath, "uploads/catalogs");
            Directory.CreateDirectory(dir);
            var fileName = $"katalog-auto-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
            var fullPath = Path.Combine(dir, fileName);

            var logoPath = Path.Combine(_env.WebRootPath, "images/logo-color.png");

            Document.Create(container =>
            {
                // ── KAPAK SAYFASI ────────────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);

                    page.Content().Column(col =>
                    {
                        // Üst koyu alan
                        col.Item().Height(580).Background(DarkHex).Padding(50).Column(inner =>
                        {
                            // Logo
                            if (File.Exists(logoPath))
                                inner.Item().Width(160).Image(logoPath);
                            else
                                inner.Item().Text("BRIKON YAPI").FontSize(22).Bold().FontColor(Colors.White);

                            inner.Item().Height(80);

                            inner.Item()
                                .Text("E-KATALOG")
                                .FontSize(52).Bold()
                                .FontColor(Colors.White)
                                .LetterSpacing(0.06f);

                            inner.Item().Height(12);

                            inner.Item()
                                .Text($"{DateTime.Now.Year} Proje Kataloğu")
                                .FontSize(16)
                                .FontColor("#94A3B8");

                            inner.Item().Height(32);

                            inner.Item().Width(80).Height(4).Background(PrimaryHex);

                            inner.Item().Height(28);

                            inner.Item()
                                .Text($"Toplam {projects.Count} Proje")
                                .FontSize(13)
                                .FontColor("#64748B");
                        });

                        // Alt açık alan
                        col.Item().Extend().Background("#F8FAFC").Padding(50).Column(inner =>
                        {
                            inner.Item().Text("Kalite ve Güvende Lider").FontSize(20).Bold().FontColor(DarkHex);
                            inner.Item().Height(12);
                            inner.Item()
                                .Text(Get("AboutText").Length > 0
                                    ? Get("AboutText")
                                    : "Brikon Yapı olarak kaliteli ve güvenilir inşaat hizmetleri sunuyoruz.")
                                .FontSize(11).FontColor(GrayHex).LineHeight(1.7f);

                            inner.Item().Extend();

                            // İletişim satırı
                            inner.Item().Row(row =>
                            {
                                if (!string.IsNullOrEmpty(Get("PhoneNumber")))
                                {
                                    row.AutoItem().Text($"☎  {Get("PhoneNumber")}").FontSize(9).FontColor(GrayHex);
                                    row.ConstantItem(24);
                                }
                                if (!string.IsNullOrEmpty(Get("Email")))
                                    row.AutoItem().Text($"✉  {Get("Email")}").FontSize(9).FontColor(GrayHex);
                            });
                        });
                    });
                });

                // ── İÇİNDEKİLER ─────────────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.Header().Element(Header);
                    page.Footer().Element(Footer);

                    page.Content().Column(col =>
                    {
                        col.Item().Text("Projelerimiz").FontSize(26).Bold().FontColor(DarkHex);
                        col.Item().Height(4).Background(PrimaryHex).Width(60);
                        col.Item().Height(24);

                        // Kategorilere göre grupla
                        var grouped = projects
                            .GroupBy(p => p.Category?.Name ?? "Diğer")
                            .OrderBy(g => g.Key);

                        foreach (var group in grouped)
                        {
                            col.Item().PaddingBottom(8).Column(grp =>
                            {
                                grp.Item().Background("#F1F5F9").Padding(8).Row(r =>
                                {
                                    r.AutoItem()
                                        .Text(group.Key)
                                        .FontSize(11).Bold()
                                        .FontColor(DarkHex);
                                    r.AutoItem().Text($"  ({group.Count()} proje)").FontSize(10).FontColor(GrayHex);
                                });

                                foreach (var p in group)
                                {
                                    grp.Item().PaddingLeft(16).PaddingVertical(3).Row(r =>
                                    {
                                        r.AutoItem().Width(6).Height(6).Background(PrimaryHex);
                                        r.ConstantItem(10);
                                        r.AutoItem().Text(p.Name).FontSize(10).FontColor("#374151");
                                        r.RelativeItem();
                                        if (!string.IsNullOrEmpty(p.City))
                                            r.AutoItem().Text(p.City).FontSize(9).FontColor(GrayHex);
                                    });
                                }
                            });
                        }
                    });
                });

                // ── PROJE SAYFALARI (2 proje / sayfa) ───────────────────────
                var chunks = projects
                    .Select((p, i) => new { p, i })
                    .GroupBy(x => x.i / 2)
                    .Select(g => g.Select(x => x.p).ToList())
                    .ToList();

                foreach (var pair in chunks)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.Header().Element(Header);
                        page.Footer().Element(Footer);

                        page.Content().Column(col =>
                        {
                            foreach (var proj in pair)
                            {
                                col.Item().PaddingBottom(20).Element(cell =>
                                    RenderProjectCard(cell, proj));
                            }
                        });
                    });
                }

                // ── KAPANIŞ SAYFASI ──────────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);

                    page.Content().Background(DarkHex).Padding(60).Column(col =>
                    {
                        col.Item().Extend();

                        if (File.Exists(logoPath))
                            col.Item().Width(140).Image(logoPath);
                        else
                            col.Item().Text("BRIKON YAPI").FontSize(20).Bold().FontColor(Colors.White);

                        col.Item().Height(30);
                        col.Item().Text("Bizimle İletişime Geçin")
                            .FontSize(26).Bold().FontColor(Colors.White);
                        col.Item().Height(6).Width(60).Background(PrimaryHex);
                        col.Item().Height(28);

                        if (!string.IsNullOrEmpty(Get("Address")))
                        {
                            col.Item().PaddingBottom(10).Text($"📍  {Get("Address")}")
                                .FontSize(11).FontColor("#94A3B8").LineHeight(1.5f);
                        }
                        if (!string.IsNullOrEmpty(Get("PhoneNumber")))
                        {
                            col.Item().PaddingBottom(10).Text($"☎   {Get("PhoneNumber")}")
                                .FontSize(11).FontColor("#94A3B8");
                        }
                        if (!string.IsNullOrEmpty(Get("Email")))
                        {
                            col.Item().PaddingBottom(10).Text($"✉   {Get("Email")}")
                                .FontSize(11).FontColor("#94A3B8");
                        }

                        col.Item().Extend();

                        col.Item().Text($"© {DateTime.Now.Year} Brikon Yapı. Tüm hakları saklıdır.")
                            .FontSize(9).FontColor("#475569");
                    });
                });
            })
            .GeneratePdf(fullPath);

            return $"/uploads/catalogs/{fileName}";
        }

        private void RenderProjectCard(IContainer container, Project proj)
        {
            var imgPath = ResolveImagePath(proj);

            container.Border(1).BorderColor("#E5E7EB").Column(col =>
            {
                // Görsel + başlık satırı
                col.Item().Row(row =>
                {
                    // Görsel
                    row.ConstantItem(180).Height(140).Element(img =>
                    {
                        if (imgPath != null && File.Exists(imgPath))
                            img.Image(imgPath).FitArea();
                        else
                            img.Background("#0F2540")
                               .AlignCenter().AlignMiddle()
                               .Text("BRIKON YAPI").FontSize(10).FontColor("#475569");
                    });

                    // Bilgi
                    row.RelativeItem().Padding(16).Column(info =>
                    {
                        // Durum badge
                        var statusColor = proj.Status == ProjectStatus.Ongoing ? PrimaryHex : "#10B981";
                        var statusText  = proj.Status == ProjectStatus.Ongoing ? "Devam Ediyor" : "Tamamlandı";
                        info.Item().PaddingBottom(6).Text(statusText)
                            .FontSize(8).Bold().FontColor(statusColor).LetterSpacing(0.08f);

                        // Proje adı
                        info.Item().PaddingBottom(4)
                            .Text(proj.Name).FontSize(13).Bold().FontColor(DarkHex);

                        // Konum
                        if (!string.IsNullOrEmpty(proj.City))
                        {
                            info.Item().PaddingBottom(8)
                                .Text($"📍 {proj.City}{(!string.IsNullOrEmpty(proj.District) ? ", " + proj.District : "")}")
                                .FontSize(9).FontColor(GrayHex);
                        }

                        // İstatistikler
                        info.Item().Row(stats =>
                        {
                            if (proj.TotalArea.HasValue)
                                StatBox(stats, proj.TotalArea.Value.ToString("N0"), "m²");
                            if (proj.UnitCount.HasValue)
                                StatBox(stats, proj.UnitCount.Value.ToString(), "Daire");
                            if (proj.FloorCount.HasValue)
                                StatBox(stats, proj.FloorCount.Value.ToString(), "Kat");
                            if (proj.BlockCount.HasValue)
                                StatBox(stats, proj.BlockCount.Value.ToString(), "Blok");
                        });

                        // Kategori
                        if (proj.Category != null)
                        {
                            info.Item().PaddingTop(6)
                                .Background("#F1F5F9").Padding(4)
                                .Text(proj.Category.Name).FontSize(8).FontColor("#64748B");
                        }
                    });
                });

                // Açıklama
                if (!string.IsNullOrWhiteSpace(proj.ShortDescription))
                {
                    col.Item()
                        .BorderTop(1).BorderColor("#F1F5F9")
                        .Padding(12)
                        .Text(proj.ShortDescription.Length > 220
                            ? proj.ShortDescription[..220] + "..."
                            : proj.ShortDescription)
                        .FontSize(9).FontColor(GrayHex).LineHeight(1.6f);
                }
            });
        }

        private static void StatBox(RowDescriptor row, string val, string lbl)
        {
            row.AutoItem().PaddingRight(14).Column(c =>
            {
                c.Item().Text(val).FontSize(13).Bold().FontColor("#0082B4");
                c.Item().Text(lbl).FontSize(7).FontColor("#9CA3AF").LetterSpacing(0.06f);
            });
        }

        private void Header(IContainer c)
        {
            var logoPath = Path.Combine(_env.WebRootPath, "images/logo-color.png");
            c.PaddingBottom(10).BorderBottom(1).BorderColor("#E5E7EB").Row(row =>
            {
                if (File.Exists(logoPath))
                    row.AutoItem().Height(24).Image(logoPath);
                else
                    row.AutoItem().Text("BRIKON YAPI").FontSize(11).Bold().FontColor(DarkHex);

                row.RelativeItem();
                row.AutoItem()
                    .Text("E-Katalog")
                    .FontSize(9).FontColor(GrayHex);
            });
        }

        private void Footer(IContainer c)
        {
            c.PaddingTop(8).BorderTop(1).BorderColor("#E5E7EB").Row(row =>
            {
                row.AutoItem()
                    .Text($"© {DateTime.Now.Year} Brikon Yapı")
                    .FontSize(8).FontColor(GrayHex);
                row.RelativeItem();
                row.AutoItem()
                    .Text(text =>
                    {
                        text.Span("Sayfa ").FontSize(8).FontColor(GrayHex);
                        text.CurrentPageNumber().FontSize(8).FontColor(GrayHex);
                        text.Span(" / ").FontSize(8).FontColor(GrayHex);
                        text.TotalPages().FontSize(8).FontColor(GrayHex);
                    });
            });
        }

        private string? ResolveImagePath(Project proj)
        {
            // 1) MainImagePath varsa onu dene
            if (!string.IsNullOrEmpty(proj.MainImagePath))
            {
                var path = Path.Combine(_env.WebRootPath, proj.MainImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return path;
            }

            // 2) Proje slug klasöründe ilk görseli dene
            var slugDir = Path.Combine(_env.WebRootPath, "images", "projects", "web", proj.Slug);
            if (Directory.Exists(slugDir))
            {
                var first = Directory.GetFiles(slugDir, "*.jpg")
                    .Concat(Directory.GetFiles(slugDir, "*.jpeg"))
                    .Concat(Directory.GetFiles(slugDir, "*.png"))
                    .OrderBy(f => f)
                    .FirstOrDefault();
                if (first != null) return first;
            }

            // 3) İlişkili Images tablosundan
            var imgRecord = proj.Images.Where(i => !i.IsPlan).OrderBy(i => i.OrderIndex).FirstOrDefault();
            if (imgRecord != null && !string.IsNullOrEmpty(imgRecord.ImagePath))
            {
                var path = Path.Combine(_env.WebRootPath, imgRecord.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return path;
            }

            return null;
        }
    }
}
