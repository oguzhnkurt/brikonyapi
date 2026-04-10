using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using BrikonYapi.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize       = 500 * 1024 * 1024; // 500 MB
    o.Limits.KeepAliveTimeout         = TimeSpan.FromMinutes(10);
    o.Limits.RequestHeadersTimeout    = TimeSpan.FromMinutes(5);
    o.Limits.MinRequestBodyDataRate   = null; // yavaş bağlantılarda zaman aşımı olmasın
});

var dbProvider = builder.Configuration["DatabaseProvider"] ?? "PostgreSQL";
var connStr    = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (dbProvider == "SqlServer")
        o.UseSqlServer(connStr);
    else
        o.UseNpgsql(connStr);
});

builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
{
    o.SignIn.RequireConfirmedAccount = false;
    o.Password.RequireDigit          = true;
    o.Password.RequiredLength        = 8;
    o.Password.RequireNonAlphanumeric= false;
    o.Password.RequireUppercase      = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath  = "/Admin/Account/Login";
    o.LogoutPath = "/Admin/Account/Logout";
    o.AccessDeniedPath = "/Admin/Account/Login";
});

builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<SiteSettingService>();
builder.Services.AddScoped<ReferenceService>();

builder.Services.AddLocalization();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 500 * 1024 * 1024; // 500 MB
    o.ValueLengthLimit         = int.MaxValue;
});
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddRazorRuntimeCompilation();

builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    var supported = new[] { new CultureInfo("tr"), new CultureInfo("en") };
    o.DefaultRequestCulture = new RequestCulture("tr");
    o.SupportedCultures     = supported;
    o.SupportedUICultures   = supported;
    o.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider()
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }

app.UseHttpsRedirection();
app.UseStaticFiles();

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "SAMEORIGIN";
    ctx.Response.Headers["X-XSS-Protection"]       = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    await next();
});
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("areas",          "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("project-detail", "Projeler/{slug}",                              new { controller = "Projects", action = "Detail" });
app.MapControllerRoute("projects",       "Projeler",                                     new { controller = "Projects", action = "Index" });
app.MapControllerRoute("default",        "{controller=Home}/{action=Index}/{id?}");

// Migrate + admin seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var um = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    const string adminEmail = "admin@brikonyapi.com";
    if (await um.FindByEmailAsync(adminEmail) == null)
        await um.CreateAsync(new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true }, "Brikon2024!");

    // ── Seed: Projeler ──────────────────────────────────────
    if (!db.Projects.Any())
    {
        db.Projects.AddRange(new[]
        {
            new Project
            {
                Name             = "Trakpark Premium",
                Slug             = "trakpark-premium",
                ShortDescription = "42 adet lüks villa ve sosyal tesis",
                Description      = "Çorlu'nun en prestijli konut projesi. Peyzajlı bahçeler, yüzme havuzu ve 7/24 güvenlik hizmeti sunulmaktadır.",
                Location         = "Çorlu, Tekirdağ",
                City             = "Tekirdağ",
                District         = "Çorlu",
                Status           = ProjectStatus.Ongoing,
                UnitCount        = 42,
                FloorCount       = 3,
                BlockCount       = 7,
                TotalArea        = 18500,
                IsActive         = true,
                IsFeatured       = true,
                IsMarquee        = true,
                StartDate        = new DateTime(2023, 3, 1),
                Latitude         = 41.1518,
                Longitude        = 27.8003
            },
            new Project
            {
                Name             = "Afyon - Gömü Tarımköy Villaları",
                Slug             = "afyon-gomu-tarimkoy-villalari",
                ShortDescription = "55 adet TOKİ tarımköy villası",
                Description      = "TOKİ işbirliğiyle hayata geçirilen bu proje, tarımsal alanların içinde modern yaşam standartlarını sunmaktadır.",
                Location         = "Gömü, Afyonkarahisar",
                City             = "Afyonkarahisar",
                District         = "Gömü",
                Status           = ProjectStatus.Completed,
                UnitCount        = 55,
                FloorCount       = 2,
                TotalArea        = 12100,
                IsActive         = true,
                IsFeatured       = true,
                StartDate        = new DateTime(2011, 8, 22),
                EndDate          = new DateTime(2013, 2, 26),
                Latitude         = 38.6321,
                Longitude        = 30.4427
            },
            new Project
            {
                Name             = "Kartaltepe Rezidans",
                Slug             = "kartaltepe-rezidans",
                ShortDescription = "120 daireli modern rezidans projesi",
                Description      = "İstanbul'un yükselen değeri Kartaltepe'de, akıllı bina sistemleri ve geniş sosyal donatılarıyla tasarlanmış prestijli bir yaşam alanı.",
                Location         = "Kartaltepe, İstanbul",
                City             = "İstanbul",
                District         = "Kartaltepe",
                Status           = ProjectStatus.Ongoing,
                UnitCount        = 120,
                FloorCount       = 12,
                BlockCount       = 2,
                TotalArea        = 34000,
                IsActive         = true,
                IsFeatured       = true,
                IsMarquee        = true,
                StartDate        = new DateTime(2024, 6, 1),
                Latitude         = 41.0082,
                Longitude        = 28.9784
            },
            new Project
            {
                Name             = "Bursa Nilüfer Konutları",
                Slug             = "bursa-nilufer-konutlari",
                ShortDescription = "68 adet yeşil sertifikalı konut",
                Description      = "LEED sertifikalı, güneş enerjisi sistemleri ve yağmur suyu geri dönüşümüyle donatılmış çevre dostu konut projesi.",
                Location         = "Nilüfer, Bursa",
                City             = "Bursa",
                District         = "Nilüfer",
                Status           = ProjectStatus.Completed,
                UnitCount        = 68,
                FloorCount       = 5,
                BlockCount       = 4,
                TotalArea        = 21500,
                IsActive         = true,
                IsFeatured       = false,
                StartDate        = new DateTime(2019, 4, 10),
                EndDate          = new DateTime(2021, 11, 30),
                Latitude         = 40.2128,
                Longitude        = 28.9779
            },
            new Project
            {
                Name             = "Edirne Sosyal Konutlar",
                Slug             = "edirne-sosyal-konutlar",
                ShortDescription = "200 birimlik sosyal konut projesi",
                Description      = "Belediye ortaklığıyla hayata geçirilen, alt ve orta gelir gruplarına yönelik, ulaşım akslarına yakın modern konut alanı.",
                Location         = "Edirne Merkez",
                City             = "Edirne",
                District         = "Merkez",
                Status           = ProjectStatus.Completed,
                UnitCount        = 200,
                FloorCount       = 8,
                BlockCount       = 5,
                TotalArea        = 52000,
                IsActive         = true,
                IsFeatured       = false,
                StartDate        = new DateTime(2016, 9, 1),
                EndDate          = new DateTime(2019, 3, 15),
                Latitude         = 41.6771,
                Longitude        = 26.5557
            }
        });
        await db.SaveChangesAsync();
    }

    // ── Seed: İletişim Mesajları ─────────────────────────────
    if (!db.ContactMessages.Any())
    {
        db.ContactMessages.AddRange(new[]
        {
            new ContactMessage
            {
                FullName  = "Murat Şahin",
                Email     = "murat.sahin@example.com",
                Phone     = "0532 111 22 33",
                Subject   = "Trakpark hakkında bilgi",
                Message   = "Trakpark Premium projesindeki villalar için fiyat ve teslim tarihi hakkında bilgi almak istiyorum.",
                CreatedAt = DateTime.Now.AddDays(-3),
                IsRead    = false
            },
            new ContactMessage
            {
                FullName  = "Elif Kaya",
                Email     = "elif.kaya@example.com",
                Phone     = "0541 333 44 55",
                Subject   = "Kurumsal işbirliği",
                Message   = "Firmamız adına birkaç proje için işbirliği görüşmesi yapmak istiyoruz. Uygun bir zaman belirleyebilir miyiz?",
                CreatedAt = DateTime.Now.AddDays(-1),
                IsRead    = false
            },
            new ContactMessage
            {
                FullName  = "Hasan Demir",
                Email     = "hasan.demir@example.com",
                Phone     = "",
                Subject   = "Genel bilgi",
                Message   = "Web sitenizden gördüm, kaliteli projeleriniz varmış. Daha detaylı katalog gönderebilir misiniz?",
                CreatedAt = DateTime.Now.AddDays(-7),
                IsRead    = true
            }
        });
        await db.SaveChangesAsync();
    }
}

app.Run();
