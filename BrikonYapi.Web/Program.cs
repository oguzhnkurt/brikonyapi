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
    o.Limits.MinRequestBodyDataRate   = null;
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
    o.MultipartBodyLengthLimit = 500 * 1024 * 1024;
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

// ── Seed helpers ────────────────────────────────────────────
var _webRoot = app.Environment.WebRootPath;

static string? MainImg(string root, string folder)
{
    var dir = Path.Combine(root, "images", "projects", "web", folder);
    if (!Directory.Exists(dir)) return null;
    var f = Directory.GetFiles(dir).OrderBy(x => x).FirstOrDefault();
    return f is null ? null : $"/images/projects/web/{folder}/{Path.GetFileName(f)}";
}

static List<ProjectImage> Gallery(string root, string folder)
{
    var dir = Path.Combine(root, "images", "projects", "web", folder);
    if (!Directory.Exists(dir)) return new();
    return Directory.GetFiles(dir).OrderBy(x => x)
        .Select((f, i) => new ProjectImage
        {
            ImagePath  = $"/images/projects/web/{folder}/{Path.GetFileName(f)}",
            IsPlan     = false,
            OrderIndex = i + 1
        }).ToList();
}

// Migrate + admin seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var um = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    const string adminEmail = "admin@brikonyapi.com";
    if (await um.FindByEmailAsync(adminEmail) == null)
        await um.CreateAsync(new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true }, "Brikon2024!");

    // ── Eski test seed'i temizle ─────────────────────────────
    if (db.Projects.Any(p => p.Slug == "kartaltepe-rezidans" || p.Slug == "bursa-nilufer-konutlari" || p.Slug == "edirne-sosyal-konutlar"))
    {
        db.ProjectImages.RemoveRange(db.ProjectImages);
        db.Projects.RemoveRange(db.Projects);
        await db.SaveChangesAsync();
    }

    // ── Seed: Gerçek Projeler ────────────────────────────────
    if (!db.Projects.Any())
    {
        var r = _webRoot;
        var projects = new List<Project>
        {
            new() {
                Name = "Trakpark Premium - 42 Adet Villa ve Sosyal Tesis", Slug = "trakpark-premium",
                ShortDescription = "Çorlu'da 42 adet lüks villa ve kapsamlı sosyal tesis projesi.",
                Description = "Çorlu/Yenice, Tekirdağ'da hayata geçirilen Trakpark Premium; peyzajlı bahçeler, yüzme havuzu ve 7/24 güvenlik hizmetiyle modern yaşam standartlarını bir araya getiriyor.",
                Location = "Çorlu, Tekirdağ", District = "Çorlu", City = "Tekirdağ",
                Status = ProjectStatus.Ongoing, UnitCount = 42,
                IsActive = true, IsFeatured = true, IsMarquee = true, OrderIndex = 1,
                Latitude = 41.1518, Longitude = 27.8003,
                MainImagePath = MainImg(r, "trakpark-premium"), Images = Gallery(r, "trakpark-premium") },

            new() {
                Name = "AFYON - Gömü 55 Adet Tarımköy Villası", Slug = "afyon-gomu-tarimkoy-villalari",
                ShortDescription = "TOKİ için Afyon Gömü köyünde 55 adet tarımköy villası.",
                Description = "T.C. Başbakanlık TOKİ iş birliğiyle Afyonkarahisar/Gömü'de inşa edilen bu proje; kırsal alanlarda modern yaşam standartlarını sunmayı hedefleyen 55 adet villa konuttan oluşmaktadır.",
                Location = "Gömü, Afyonkarahisar", District = "Gömü", City = "Afyonkarahisar",
                Status = ProjectStatus.Completed, UnitCount = 55, FloorCount = 2,
                StartDate = new DateTime(2011,8,22), EndDate = new DateTime(2013,2,26),
                IsActive = true, IsMarquee = true, OrderIndex = 2,
                MainImagePath = MainImg(r, "afyon-gomu-tarimkoy-villalari"), Images = Gallery(r, "afyon-gomu-tarimkoy-villalari") },

            new() {
                Name = "AFYON - Serban 55 Adet Tarımköy Villası", Slug = "toki-afyon-serbanli-tarimkoy",
                ShortDescription = "TOKİ için Afyon Serban köyünde 55 adet tarımköy villası.",
                Description = "T.C. Başbakanlık TOKİ iş birliğiyle Afyonkarahisar/Serban'da inşa edilen, tarımsal alanlarda modern yaşam sunan 55 villa projesi.",
                Location = "Serban, Afyonkarahisar", District = "Serban", City = "Afyonkarahisar",
                Status = ProjectStatus.Completed, UnitCount = 55, FloorCount = 2,
                StartDate = new DateTime(2011,8,22), EndDate = new DateTime(2013,2,26),
                IsActive = true, IsMarquee = true, OrderIndex = 3,
                MainImagePath = MainImg(r, "toki-afyon-serbanli-tarimkoy"), Images = Gallery(r, "toki-afyon-serbanli-tarimkoy") },

            new() {
                Name = "TOKİ Zonguldak Devrek 100 Yataklı Devlet Hastanesi", Slug = "devlet-hastanesi-zonguldak",
                ShortDescription = "Zonguldak Devrek'te TOKİ için 100 yataklı devlet hastanesi ve çevre düzenlemesi.",
                Description = "600 adet (Ø1m, 22m uzunluk) kazıklı temel üzerine inşa edilen 22.500 m² alanlı yeni devlet hastanesi; altyapı ve çevre düzenlemesini kapsamaktadır.",
                Location = "Devrek, Zonguldak", District = "Devrek", City = "Zonguldak",
                Status = ProjectStatus.Completed, TotalArea = 22500,
                StartDate = new DateTime(2011,11,17), EndDate = new DateTime(2014,8,17),
                IsActive = true, OrderIndex = 4,
                MainImagePath = MainImg(r, "devlet-hastanesi-zonguldak"), Images = Gallery(r, "devlet-hastanesi-zonguldak") },

            new() {
                Name = "Hacettepe Üniversitesi Beytepe Kongre Merkezi", Slug = "hacettepe-kongre-merkezi",
                ShortDescription = "3.000 kişilik kapasitesiyle Beytepe Kampüsü'nde modern kongre merkezi.",
                Description = "Hacettepe Üniversitesi Rektörlüğü için 22.000 m² alanda inşa edilen kongre merkezi; 3.000 kişilik ana salon, 8 toplantı salonu (25-100 kişi), balo salonu (500 kişi), mutfak ve fuaye alanlarından oluşmaktadır.",
                Location = "Beytepe, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 22000,
                StartDate = new DateTime(2007,9,28), EndDate = new DateTime(2012,4,13),
                IsActive = true, OrderIndex = 5,
                MainImagePath = MainImg(r, "hacettepe-kongre-merkezi"), Images = Gallery(r, "hacettepe-kongre-merkezi") },

            new() {
                Name = "Hacettepe Üniversitesi Makina Mühendisliği Bölümü", Slug = "hacettepe-makina-muhendisligi",
                ShortDescription = "Beytepe Kampüsü'nde 17.000 m² eğitim ve idare binaları.",
                Description = "Hacettepe Üniversitesi Rektörlüğü adına Beytepe Kampüsü'nde inşa edilen 17.000 m² alanlı Makina Mühendisliği Bölümü eğitim ve idari binaları.",
                Location = "Beytepe, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 17000,
                StartDate = new DateTime(2005,8,11), EndDate = new DateTime(2008,5,31),
                IsActive = true, OrderIndex = 6,
                MainImagePath = MainImg(r, "hacettepe-makina-muhendisligi"), Images = Gallery(r, "hacettepe-makina-muhendisligi") },

            new() {
                Name = "Hacettepe Üniversitesi Yabancı Diller Yüksekokulu", Slug = "hacettepe-yabanci-diller",
                ShortDescription = "Beytepe Kampüsü'nde 10.500 m² yabancı diller yüksekokulu binası.",
                Description = "Hacettepe Üniversitesi bünyesinde Beytepe Kampüsü'nde inşa edilen 10.500 m² alanlı Yabancı Diller Yüksekokulu eğitim binası.",
                Location = "Beytepe, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 10500,
                EndDate = new DateTime(2003,12,31),
                IsActive = true, OrderIndex = 7,
                MainImagePath = MainImg(r, "hacettepe-yabanci-diller"), Images = Gallery(r, "hacettepe-yabanci-diller") },

            new() {
                Name = "Safranbolu Güzel Sanatlar Lisesi", Slug = "safranbolu-guzel-sanatlar-lisesi",
                ShortDescription = "Karabük Safranbolu'da 19.000 m² MEB güzel sanatlar lisesi kampüsü.",
                Description = "T.C. Milli Eğitim Bakanlığı için inşa edilen 19.000 m² alanlı güzel sanatlar lisesi; eğitim binaları, erkek-kız öğrenci yurtları, açık spor alanları ve peyzaj düzenlemesini kapsamaktadır.",
                Location = "Safranbolu, Karabük", District = "Safranbolu", City = "Karabük",
                Status = ProjectStatus.Completed, TotalArea = 19000,
                StartDate = new DateTime(2002,2,12), EndDate = new DateTime(2002,11,30),
                IsActive = true, OrderIndex = 8,
                MainImagePath = MainImg(r, "safranbolu-guzel-sanatlar-lisesi"), Images = Gallery(r, "safranbolu-guzel-sanatlar-lisesi") },

            new() {
                Name = "Hacettepe Üniversitesi Diş Hekimliği Fakültesi Büyük Onarım", Slug = "hacettepe-dis-hekimligi",
                ShortDescription = "Ankara Sıhhiye'de 8.500 m² diş hekimliği fakültesi büyük onarımı.",
                Description = "Hacettepe Üniversitesi Rektörlüğü için Sıhhiye Kampüsü'nde gerçekleştirilen 8.500 m² alanlı Diş Hekimliği Fakültesi büyük onarım ve yenileme projesi.",
                Location = "Sıhhiye, Ankara", District = "Altındağ", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 8500,
                StartDate = new DateTime(1998,12,28), EndDate = new DateTime(2001,10,25),
                IsActive = true, OrderIndex = 9,
                MainImagePath = MainImg(r, "hacettepe-dis-hekimligi"), Images = Gallery(r, "hacettepe-dis-hekimligi") },

            new() {
                Name = "Hacettepe Üniversitesi Hastaneleri Çevre Düzenlemesi", Slug = "hacettepe-hastaneleri-cevre",
                ShortDescription = "Hacettepe Üniversitesi Hastaneleri peyzaj ve çevre düzenlemesi.",
                Description = "Hacettepe Üniversitesi Rektörlüğü adına gerçekleştirilen 5.500 m² alanlı hastane tesisleri çevre düzenlemesi ve peyzaj projesi.",
                Location = "Sıhhiye, Ankara", District = "Altındağ", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 5500,
                EndDate = new DateTime(2000,12,31),
                IsActive = true, OrderIndex = 10,
                MainImagePath = MainImg(r, "hacettepe-hastaneleri-cevre"), Images = Gallery(r, "hacettepe-hastaneleri-cevre") },

            new() {
                Name = "Hacettepe Üniversitesi Morfoloji Binası Dış Cephe Onarımı", Slug = "hacettepe-morfoloji-dis-cephe",
                ShortDescription = "Mikrobiyoloji, Farmakoloji ve Patoloji bölümleri dış cephe onarımı.",
                Description = "Hacettepe Üniversitesi Rektörlüğü adına Mikrobiyoloji, Farmakoloji ve Patoloji Anabilim Dalları binalarının 4.500 m² alanlı dış cephe onarım projesi.",
                Location = "Sıhhiye, Ankara", District = "Altındağ", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 4500,
                StartDate = new DateTime(2004,7,20), EndDate = new DateTime(2004,8,3),
                IsActive = true, OrderIndex = 11,
                MainImagePath = MainImg(r, "hacettepe-morfoloji-dis-cephe"), Images = Gallery(r, "hacettepe-morfoloji-dis-cephe") },

            new() {
                Name = "Hacettepe Üniversitesi Beytepe Otomotiv Mühendisliği Bölümü", Slug = "hacettepe-beytepe-otomotiv",
                ShortDescription = "Beytepe Kampüsü'nde 2.300 m² otomotiv mühendisliği bölümü binası.",
                Description = "Hacettepe Üniversitesi Rektörlüğü adına Beytepe Kampüsü içinde inşa edilen 2.300 m² alanlı Otomotiv Mühendisliği Bölümü binası.",
                Location = "Beytepe, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 2300,
                EndDate = new DateTime(2007,12,31),
                IsActive = true, OrderIndex = 12,
                MainImagePath = MainImg(r, "hacettepe-beytepe-otomotiv"), Images = Gallery(r, "hacettepe-beytepe-otomotiv") },

            new() {
                Name = "Hacettepe Üniversitesi Kafeterya Binası", Slug = "hacettepe-kafeterya",
                ShortDescription = "Hacettepe Üniversitesi kampüsünde 650 m² kafeterya binası.",
                Description = "Hacettepe Üniversitesi Rektörlüğü adına 650 m² alanlı modern kafeterya binası inşaatı.",
                Location = "Beytepe, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 650,
                EndDate = new DateTime(2003,12,31),
                IsActive = true, OrderIndex = 13,
                MainImagePath = MainImg(r, "hacettepe-kafeterya"), Images = Gallery(r, "hacettepe-kafeterya") },

            new() {
                Name = "Zonguldak Karaelmas Üniversitesi Alaplı MYO ve Lojman", Slug = "zonguldak-uni-alapli-myo",
                ShortDescription = "Zonguldak Alaplı'da 8.500 m² meslek yüksekokulu ve lojman binası.",
                Description = "Zonguldak Karaelmas Üniversitesi Rektörlüğü adına Alaplı ilçesinde 8.500 m² alanlı Meslek Yüksekokulu ve lojman binası inşaatı.",
                Location = "Alaplı, Zonguldak", District = "Alaplı", City = "Zonguldak",
                Status = ProjectStatus.Completed, TotalArea = 8500,
                EndDate = new DateTime(1998,12,31),
                IsActive = true, OrderIndex = 14,
                MainImagePath = MainImg(r, "zonguldak-uni-alapli-myo"), Images = Gallery(r, "zonguldak-uni-alapli-myo") },

            new() {
                Name = "İGA İstanbul Yeni Havalimanı Akaryakıt Yönetim Binası", Slug = "istanbul-havalimani-akaryakit-bina",
                ShortDescription = "İstanbul Havalimanı akaryakıt operasyonları yönetim binası.",
                Description = "İGA işletmesi bünyesinde İstanbul Yeni Havalimanı sahasında inşa edilen akaryakıt yönetim ve operasyon binası.",
                Location = "Arnavutköy, İstanbul", District = "Arnavutköy", City = "İstanbul",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 15,
                MainImagePath = MainImg(r, "istanbul-havalimani-akaryakit-bina"), Images = Gallery(r, "istanbul-havalimani-akaryakit-bina") },

            new() {
                Name = "İGA İstanbul 3. Havalimanı Cami İnce İşleri", Slug = "istanbul-havalimani-camii",
                ShortDescription = "İstanbul 3. Havalimanı camisinin iç mekan ince işleri.",
                Description = "İGA bünyesinde İstanbul 3. Havalimanı içinde yer alan caminin tüm ince yapı işleri, dekorasyon ve tamamlama çalışmalarının yürütülmesi.",
                Location = "Arnavutköy, İstanbul", District = "Arnavutköy", City = "İstanbul",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 16,
                MainImagePath = MainImg(r, "istanbul-havalimani-camii"), Images = Gallery(r, "istanbul-havalimani-camii") },

            new() {
                Name = "Ankara Ümitköy Villa İnşaatları", Slug = "ankara-imitkoy-villa",
                ShortDescription = "Ankara Ümitköy'de özel sektör için villa inşaatları.",
                Description = "Ankara Ümitköy'de özel sektör için gerçekleştirilen 1.100 m² alanlı müstakil villa inşaatları projesi.",
                Location = "Ümitköy, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed, TotalArea = 1100,
                EndDate = new DateTime(2003,12,31),
                IsActive = true, OrderIndex = 17,
                MainImagePath = MainImg(r, "ankara-imitkoy-villa"), Images = Gallery(r, "ankara-imitkoy-villa") },

            new() {
                Name = "Bakırköy Şenlik Mahallesi Bilgiç Apartmanı", Slug = "bakirkoy-senlik-mahallesi-bilgic-apartmani",
                ShortDescription = "Bakırköy Şenlik Mahallesi'nde apartman inşaatı.",
                Description = "İstanbul Bakırköy Şenlik Mahallesi'nde gerçekleştirilen konut apartmanı inşaat projesi.",
                Location = "Bakırköy, İstanbul", District = "Bakırköy", City = "İstanbul",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 18,
                MainImagePath = MainImg(r, "bakirkoy-senlik-mahallesi-bilgic-apartmani"), Images = Gallery(r, "bakirkoy-senlik-mahallesi-bilgic-apartmani") },

            new() {
                Name = "Beşiktaş Yıldız Mahallesi Mor Apartmanı", Slug = "besiktas-yildiz-mahallesi-mor-apartmani",
                ShortDescription = "Beşiktaş Yıldız Mahallesi'nde konut apartmanı.",
                Description = "İstanbul Beşiktaş Yıldız Mahallesi'nde inşa edilen modern konut apartmanı projesi.",
                Location = "Beşiktaş, İstanbul", District = "Beşiktaş", City = "İstanbul",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 19,
                MainImagePath = MainImg(r, "besiktas-yildiz-mahallesi-mor-apartmani"), Images = Gallery(r, "besiktas-yildiz-mahallesi-mor-apartmani") },

            new() {
                Name = "Çankaya MKB Mesleki ve Teknik Lisesi", Slug = "cankaya-mkb-mesleki-teknik-lisesi",
                ShortDescription = "Ankara Çankaya'da mesleki ve teknik lise binası.",
                Description = "Ankara Çankaya'da inşa edilen MKB Mesleki ve Teknik Lisesi okul binaları inşaat projesi.",
                Location = "Çankaya, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 20,
                MainImagePath = MainImg(r, "cankaya-mkb-mesleki-teknik-lisesi"), Images = Gallery(r, "cankaya-mkb-mesleki-teknik-lisesi") },

            new() {
                Name = "Çukurambar 30 Daireli Konut Projesi", Slug = "cukurambar-30-daire",
                ShortDescription = "Ankara Çukurambar'da 30 daireli konut projesi.",
                Description = "Ankara Çukurambar'da hayata geçirilen 30 daireli modern konut projesi.",
                Location = "Çukurambar, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed, UnitCount = 30,
                IsActive = true, OrderIndex = 21,
                MainImagePath = MainImg(r, "cukurambar-30-daire"), Images = Gallery(r, "cukurambar-30-daire") },

            new() {
                Name = "Çukurambar 32 Daireli Konut Projesi", Slug = "cukurambar-32-daire",
                ShortDescription = "Ankara Çukurambar'da 32 daireli konut projesi.",
                Description = "Ankara Çukurambar'da hayata geçirilen 32 daireli modern konut projesi.",
                Location = "Çukurambar, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed, UnitCount = 32,
                IsActive = true, OrderIndex = 22,
                MainImagePath = MainImg(r, "cukurambar-32-daire"), Images = Gallery(r, "cukurambar-32-daire") },

            new() {
                Name = "Hacettepe Üniversitesi Altyapı ve Kanalizasyon Çalışmaları", Slug = "hacettepe-altyapi-kanalizasyon",
                ShortDescription = "Hacettepe Üniversitesi kampüs altyapı ve kanalizasyon yenileme.",
                Description = "Hacettepe Üniversitesi Rektörlüğü adına yürütülen kampüs altyapı iyileştirme ve kanalizasyon sistemi yenileme çalışmaları.",
                Location = "Beytepe, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 23,
                MainImagePath = MainImg(r, "hacettepe-altyapi-kanalizasyon"), Images = Gallery(r, "hacettepe-altyapi-kanalizasyon") },

            new() {
                Name = "Hacettepe Üniversitesi Beytepe Anaokulu", Slug = "hacettepe-beytepe-anaokulu",
                ShortDescription = "Beytepe Kampüsü'nde anaokulu binası inşaatı.",
                Description = "Hacettepe Üniversitesi Rektörlüğü adına Beytepe Kampüsü'nde inşa edilen anaokulu binası.",
                Location = "Beytepe, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 24,
                MainImagePath = MainImg(r, "hacettepe-beytepe-anaokulu"), Images = Gallery(r, "hacettepe-beytepe-anaokulu") },

            new() {
                Name = "Hacettepe Üniversitesi Jeodezi ve Fotogrametri Bölümü", Slug = "hacettepe-jeodezi-fotogrametri",
                ShortDescription = "Hacettepe Üniversitesi jeodezi ve fotogrametri bölüm binası.",
                Description = "Hacettepe Üniversitesi Rektörlüğü adına Beytepe Kampüsü'nde inşa edilen Jeodezi ve Fotogrametri Mühendisliği Bölümü binası.",
                Location = "Beytepe, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 25,
                MainImagePath = MainImg(r, "hacettepe-jeodezi-fotogrametri"), Images = Gallery(r, "hacettepe-jeodezi-fotogrametri") },

            new() {
                Name = "Haymana Bumsuz İlköğretim Okulu", Slug = "haymana-bumsuz-ilkogretim",
                ShortDescription = "Ankara Haymana Bumsuz köyünde ilköğretim okulu.",
                Description = "Ankara Haymana ilçesi Bumsuz köyünde inşa edilen ilköğretim okulu binası.",
                Location = "Haymana, Ankara", District = "Haymana", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 26,
                MainImagePath = MainImg(r, "haymana-bumsuz-ilkogretim"), Images = Gallery(r, "haymana-bumsuz-ilkogretim") },

            new() {
                Name = "Haymana Oyaca İlköğretim Okulu", Slug = "haymana-oyaca-ilkogretim",
                ShortDescription = "Ankara Haymana Oyaca köyünde ilköğretim okulu.",
                Description = "Ankara Haymana ilçesi Oyaca köyünde inşa edilen ilköğretim okulu binası.",
                Location = "Haymana, Ankara", District = "Haymana", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 27,
                MainImagePath = MainImg(r, "haymana-oyaca-ilkogretim"), Images = Gallery(r, "haymana-oyaca-ilkogretim") },

            new() {
                Name = "Ihlamur Apartmanı", Slug = "ihlamur-apartmani",
                ShortDescription = "Modern konut apartmanı inşaatı.",
                Description = "Özel sektör için gerçekleştirilen modern konut apartmanı inşaat projesi.",
                Location = "Ankara", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 28,
                MainImagePath = MainImg(r, "ihlamur-apartmani"), Images = Gallery(r, "ihlamur-apartmani") },

            new() {
                Name = "Karabük Üniversitesi Kongre Salonu", Slug = "karabuk-uni-kongre-salonu",
                ShortDescription = "Karabük Üniversitesi'nde kongre salonu inşaatı.",
                Description = "Karabük Üniversitesi bünyesinde inşa edilen kongre ve etkinlik salonu binası.",
                Location = "Karabük", City = "Karabük",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 29,
                MainImagePath = MainImg(r, "karabuk-uni-kongre-salonu"), Images = Gallery(r, "karabuk-uni-kongre-salonu") },

            new() {
                Name = "Karabük Üniversitesi Teknik Eğitim Binası", Slug = "karabuk-uni-teknik-egitim",
                ShortDescription = "Karabük Üniversitesi teknik eğitim ve laboratuvar binası.",
                Description = "Karabük Üniversitesi bünyesinde inşa edilen teknik eğitim, atölye ve laboratuvar binası.",
                Location = "Karabük", City = "Karabük",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 30,
                MainImagePath = MainImg(r, "karabuk-uni-teknik-egitim"), Images = Gallery(r, "karabuk-uni-teknik-egitim") },

            new() {
                Name = "Karabük Üniversitesi Bina İnşaatı", Slug = "karabuk-universitesi",
                ShortDescription = "Karabük Üniversitesi kampüs bina inşaatı.",
                Description = "Karabük Üniversitesi kampüsünde gerçekleştirilen çok amaçlı bina inşaat projesi.",
                Location = "Karabük", City = "Karabük",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 31,
                MainImagePath = MainImg(r, "karabuk-universitesi"), Images = Gallery(r, "karabuk-universitesi") },

            new() {
                Name = "Muğla Bodrum Değirmentepe Konut Projesi", Slug = "mugla-bodrum-degirmentepe",
                ShortDescription = "Bodrum Değirmentepe'de özel konut projesi.",
                Description = "Muğla Bodrum Değirmentepe mevkiinde gerçekleştirilen özel sektör konut projesi.",
                Location = "Bodrum, Muğla", District = "Bodrum", City = "Muğla",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 32,
                MainImagePath = MainImg(r, "mugla-bodrum-degirmentepe"), Images = Gallery(r, "mugla-bodrum-degirmentepe") },

            new() {
                Name = "Muğla Bodrum Firuze Taş Evler", Slug = "mugla-bodrum-firuze-tas-evler",
                ShortDescription = "Bodrum'da geleneksel taş ev mimarisiyle özel konut projesi.",
                Description = "Muğla Bodrum'da geleneksel Ege mimarisini modern konforla buluşturan taş ev konut projesi.",
                Location = "Bodrum, Muğla", District = "Bodrum", City = "Muğla",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 33,
                MainImagePath = MainImg(r, "mugla-bodrum-firuze-tas-evler"), Images = Gallery(r, "mugla-bodrum-firuze-tas-evler") },

            new() {
                Name = "ODTÜ Parlar Öğrenci Yurdu", Slug = "odtu-parlar-ogrenci-yurdu",
                ShortDescription = "ODTÜ kampüsünde öğrenci yurdu inşaatı.",
                Description = "ODTÜ Ankara kampüsünde inşa edilen Parlar öğrenci yurdu binası.",
                Location = "Çankaya, Ankara", District = "Çankaya", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 34,
                MainImagePath = MainImg(r, "odtu-parlar-ogrenci-yurdu"), Images = Gallery(r, "odtu-parlar-ogrenci-yurdu") },

            new() {
                Name = "Safranbolu Hatice Sultan Konağı Restorasyonu", Slug = "safranbolu-hatice-sultan-konagi",
                ShortDescription = "Safranbolu'da tarihi Hatice Sultan Konağı restorasyon projesi.",
                Description = "Karabük Safranbolu'da UNESCO Dünya Mirası alanında yer alan tarihi Hatice Sultan Konağı'nın koruma amaçlı restorasyon çalışmaları.",
                Location = "Safranbolu, Karabük", District = "Safranbolu", City = "Karabük",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 35,
                MainImagePath = MainImg(r, "safranbolu-hatice-sultan-konagi"), Images = Gallery(r, "safranbolu-hatice-sultan-konagi") },

            new() {
                Name = "Türkan Hanım Apartmanı", Slug = "turkan-hanim-apartmani",
                ShortDescription = "Özel sektör için konut apartmanı inşaatı.",
                Description = "Özel sektör için gerçekleştirilen modern konut apartmanı inşaat projesi.",
                Location = "Ankara", City = "Ankara",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 36,
                MainImagePath = MainImg(r, "turkan-hanim-apartmani"), Images = Gallery(r, "turkan-hanim-apartmani") },

            new() {
                Name = "Zonguldak Üniversitesi Yemekhane İnşaatı", Slug = "zonguldak-uni-yemekhane",
                ShortDescription = "Zonguldak Üniversitesi yemekhane binası.",
                Description = "Zonguldak Üniversitesi bünyesinde inşa edilen merkezi yemekhane ve sosyal tesis binası.",
                Location = "Zonguldak", City = "Zonguldak",
                Status = ProjectStatus.Completed,
                IsActive = true, OrderIndex = 37,
                MainImagePath = MainImg(r, "zonguldak-uni-yemekhane"), Images = Gallery(r, "zonguldak-uni-yemekhane") },
        };

        db.Projects.AddRange(projects);
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
