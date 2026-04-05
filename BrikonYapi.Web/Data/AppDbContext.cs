using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Data
{
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectImage> ProjectImages { get; set; }
        public DbSet<HeroSlide> HeroSlides { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; }
        public DbSet<Reference> References { get; set; }
        public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Project>(e =>
            {
                e.HasIndex(p => p.Slug).IsUnique();
                e.HasMany(p => p.Images).WithOne(i => i.Project).HasForeignKey(i => i.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(p => p.HeroSlides).WithOne(h => h.Project).HasForeignKey(h => h.ProjectId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(p => p.Category).WithMany(c => c.Projects).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Kampüs - Eğitim Yapıları",  OrderIndex = 0 },
                new Category { Id = 2, Name = "Konutlar - Ofisler",         OrderIndex = 1 },
                new Category { Id = 3, Name = "Sağlık Yapıları",            OrderIndex = 2 },
                new Category { Id = 4, Name = "Endüstriyel Yapılar",        OrderIndex = 3 },
                new Category { Id = 5, Name = "Altyapı - Yol",              OrderIndex = 4 },
                new Category { Id = 6, Name = "Renovasyonlar",              OrderIndex = 5 },
                new Category { Id = 7, Name = "Büyükelçilikler",            OrderIndex = 6 },
                new Category { Id = 8, Name = "İdari Binalar",              OrderIndex = 7 }
            );

            builder.Entity<SiteSetting>(e => e.HasIndex(s => s.Key).IsUnique());

            builder.Entity<SiteSetting>().HasData(
                new SiteSetting { Id = 1, Key = "PhoneNumber",    Value = "+90 555 000 00 00",   Description = "Header telefon" },
                new SiteSetting { Id = 2, Key = "Email",          Value = "info@brikonyapi.com", Description = "E-posta" },
                new SiteSetting { Id = 3, Key = "Address",        Value = "İstanbul, Türkiye",   Description = "Adres" },
                new SiteSetting { Id = 4, Key = "Instagram",      Value = "",                    Description = "Instagram URL" },
                new SiteSetting { Id = 5, Key = "Facebook",       Value = "",                    Description = "Facebook URL" },
                new SiteSetting { Id = 6, Key = "LinkedIn",       Value = "",                    Description = "LinkedIn URL" },
                new SiteSetting { Id = 7, Key = "CompanySlogan",  Value = "Geleceği İnşa Ediyoruz", Description = "Slogan" },
                new SiteSetting { Id = 8, Key = "AboutText",      Value = "Brikon Yapı olarak kaliteli ve güvenilir inşaat hizmetleri sunuyoruz.", Description = "Hakkımızda metni" },
                new SiteSetting { Id = 9, Key = "WhatsApp",       Value = "",                    Description = "WhatsApp numarası" }
            );
        }
    }
}
