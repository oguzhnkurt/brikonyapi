using BrikonYapi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Data
{
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectImage> ProjectImages { get; set; }
        public DbSet<HeroSlide> HeroSlides { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Project>(e =>
            {
                e.HasIndex(p => p.Slug).IsUnique();
                e.HasMany(p => p.Images)
                    .WithOne(i => i.Project)
                    .HasForeignKey(i => i.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(p => p.HeroSlides)
                    .WithOne(h => h.Project)
                    .HasForeignKey(h => h.ProjectId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<SiteSetting>(e =>
            {
                e.HasIndex(s => s.Key).IsUnique();
            });

            // Seed default site settings
            builder.Entity<SiteSetting>().HasData(
                new SiteSetting { Id = 1, Key = "PhoneNumber", Value = "+90 555 000 00 00", Description = "Header telefon numarası" },
                new SiteSetting { Id = 2, Key = "Email", Value = "info@brikonyapi.com", Description = "İletişim e-posta" },
                new SiteSetting { Id = 3, Key = "Address", Value = "İstanbul, Türkiye", Description = "Şirket adresi" },
                new SiteSetting { Id = 4, Key = "Instagram", Value = "", Description = "Instagram profil URL" },
                new SiteSetting { Id = 5, Key = "Facebook", Value = "", Description = "Facebook profil URL" },
                new SiteSetting { Id = 6, Key = "LinkedIn", Value = "", Description = "LinkedIn profil URL" },
                new SiteSetting { Id = 7, Key = "CompanySlogan", Value = "Geleceği İnşa Ediyoruz", Description = "Şirket sloganı" },
                new SiteSetting { Id = 8, Key = "AboutText", Value = "Brikon Yapı olarak, yılların verdiği tecrübe ile kaliteli ve güvenilir inşaat hizmetleri sunuyoruz.", Description = "Hakkımızda kısa metin" }
            );
        }
    }
}
