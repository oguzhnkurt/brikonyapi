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
        public DbSet<Certificate> Certificates { get; set; }
        public DbSet<Catalog> Catalogs { get; set; }

        // ── Kat Malikleri Ödeme Portalı ──────────────────────────
        public DbSet<Owner> Owners { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<PaymentSchedule> PaymentSchedules { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<PaymentPlanTemplate> PaymentPlanTemplates { get; set; }
        public DbSet<PaymentPlanTemplateItem> PaymentPlanTemplateItems { get; set; }
        public DbSet<NotificationLog> NotificationLogs { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<FaqItem> Faqs { get; set; }

        // ── Kat Maliki Portalı: ilerleme, oylama, sohbet ─────────
        public DbSet<ProjectStage> ProjectStages { get; set; }
        public DbSet<SitePhoto> SitePhotos { get; set; }
        public DbSet<Poll> Polls { get; set; }
        public DbSet<PollOption> PollOptions { get; set; }
        public DbSet<PollVote> PollVotes { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ChatPoll> ChatPolls { get; set; }
        public DbSet<ChatPollOption> ChatPollOptions { get; set; }
        public DbSet<ChatPollVote> ChatPollVotes { get; set; }
        public DbSet<OwnerNotificationPreference> OwnerNotificationPreferences { get; set; }
        public DbSet<OwnerProjectAccess> OwnerProjectAccesses { get; set; }

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

            // ── Kat Malikleri Ödeme Portalı ──────────────────────────
            builder.Entity<Owner>(e =>
            {
                e.HasIndex(o => o.UserId).IsUnique();
            });

            builder.Entity<Unit>(e =>
            {
                e.HasIndex(u => new { u.ProjectId, u.UnitNo }).IsUnique();
                e.HasOne(u => u.Project).WithMany().HasForeignKey(u => u.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(u => u.Owner).WithMany(o => o.Units).HasForeignKey(u => u.OwnerId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<PaymentSchedule>(e =>
            {
                e.HasOne(p => p.Unit).WithMany(u => u.PaymentSchedules).HasForeignKey(p => p.UnitId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.ProjectStage).WithMany().HasForeignKey(p => p.ProjectStageId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<PaymentTransaction>(e =>
            {
                e.HasOne(t => t.PaymentSchedule).WithMany(p => p.Transactions).HasForeignKey(t => t.PaymentScheduleId).OnDelete(DeleteBehavior.Cascade);
            });

            // ── Ödeme Planı Şablonları (proje bazlı, gruba toplu atanabilir) ──
            builder.Entity<PaymentPlanTemplate>(e =>
            {
                e.HasOne(t => t.Project).WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(t => t.ProjectId);
            });

            builder.Entity<PaymentPlanTemplateItem>(e =>
            {
                e.HasOne(i => i.PaymentPlanTemplate).WithMany(t => t.Items).HasForeignKey(i => i.PaymentPlanTemplateId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(i => i.ProjectStage).WithMany().HasForeignKey(i => i.ProjectStageId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(i => new { i.PaymentPlanTemplateId, i.OrderIndex });
            });

            builder.Entity<NotificationLog>(e =>
            {
                e.HasOne(n => n.Owner).WithMany().HasForeignKey(n => n.OwnerId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(n => n.RelatedPaymentSchedule).WithMany().HasForeignKey(n => n.RelatedPaymentScheduleId).OnDelete(DeleteBehavior.SetNull);
            });

            // ── İlerleme: aşamalar ve saha fotoğrafları ──────────────
            builder.Entity<ProjectStage>(e =>
            {
                e.HasOne(s => s.Project).WithMany(p => p.Stages).HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(s => new { s.ProjectId, s.OrderIndex });
            });

            builder.Entity<SitePhoto>(e =>
            {
                e.HasOne(s => s.Project).WithMany(p => p.SitePhotos).HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(s => new { s.ProjectId, s.TakenAt });
            });

            // ── Oylama ───────────────────────────────────────────────
            builder.Entity<Poll>(e =>
            {
                e.HasOne(p => p.Project).WithMany().HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(p => p.Options).WithOne(o => o.Poll).HasForeignKey(o => o.PollId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PollVote>(e =>
            {
                e.HasOne(v => v.Poll).WithMany(p => p.Votes).HasForeignKey(v => v.PollId).OnDelete(DeleteBehavior.Cascade);
                // Secenek silinirse oy kaydi da silinmeli; ancak Poll uzerinden zaten cascade geldigi icin
                // cift cascade yolu olusmasin diye burada Restrict kullaniyoruz.
                e.HasOne(v => v.PollOption).WithMany(o => o.Votes).HasForeignKey(v => v.PollOptionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(v => v.Owner).WithMany().HasForeignKey(v => v.OwnerId).OnDelete(DeleteBehavior.Cascade);

                // Bir malik bir oylamada yalnizca bir kez oy kullanabilir (veritabani seviyesinde garanti).
                e.HasIndex(v => new { v.PollId, v.OwnerId }).IsUnique();
            });

            // ── Sohbet ───────────────────────────────────────────────
            builder.Entity<ChatMessage>(e =>
            {
                e.HasOne(m => m.Project).WithMany().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(m => m.Owner).WithMany().HasForeignKey(m => m.OwnerId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(m => m.ChatPoll).WithMany().HasForeignKey(m => m.ChatPollId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(m => new { m.ProjectId, m.CreatedAt });
            });

            // ── Sohbet anketi (WhatsApp tarzı, sohbet akışı içinde) ──
            builder.Entity<ChatPoll>(e =>
            {
                e.HasOne(p => p.Project).WithMany().HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(p => p.Options).WithOne(o => o.ChatPoll).HasForeignKey(o => o.ChatPollId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ChatPollVote>(e =>
            {
                e.HasOne(v => v.ChatPoll).WithMany(p => p.Votes).HasForeignKey(v => v.ChatPollId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(v => v.ChatPollOption).WithMany(o => o.Votes).HasForeignKey(v => v.ChatPollOptionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(v => v.Owner).WithMany().HasForeignKey(v => v.OwnerId).OnDelete(DeleteBehavior.Cascade);

                // Bir malik bir sohbet anketinde yalnizca bir kez oy kullanabilir (oyunu degistirebilir).
                e.HasIndex(v => new { v.ChatPollId, v.OwnerId }).IsUnique();
            });

            // ── Bildirim tercihleri (Owner ile 1:1) ──────────────────
            builder.Entity<OwnerNotificationPreference>(e =>
            {
                e.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(p => p.OwnerId).IsUnique();
            });

            // ── Malik başına proje/sohbet erişimi (admin tarafından tek tek atanır) ──
            builder.Entity<OwnerProjectAccess>(e =>
            {
                e.HasOne(a => a.Owner).WithMany().HasForeignKey(a => a.OwnerId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(a => new { a.OwnerId, a.ProjectId }).IsUnique();
            });

            builder.Entity<SiteSetting>(e => e.HasIndex(s => s.Key).IsUnique());

            builder.Entity<SiteSetting>().HasData(
                new SiteSetting { Id = 1, Key = "PhoneNumber",    Value = "+90 212 236 19 88",   Description = "Header telefon" },
                new SiteSetting { Id = 2, Key = "Email",          Value = "info@brikonyapi.com", Description = "E-posta" },
                new SiteSetting { Id = 3, Key = "Address",        Value = "Seyrantepe Mahallesi İbrahim Karaoğlanoğlu Cad. No:147 K:7 KAĞITHANE-İSTANBUL", Description = "Adres" },
                new SiteSetting { Id = 4, Key = "Instagram",      Value = "",                    Description = "Instagram URL" },
                new SiteSetting { Id = 5, Key = "Facebook",       Value = "",                    Description = "Facebook URL" },
                new SiteSetting { Id = 6, Key = "LinkedIn",       Value = "",                    Description = "LinkedIn URL" },
                new SiteSetting { Id = 7, Key = "CompanySlogan",  Value = "Geleceği İnşa Ediyoruz", Description = "Slogan" },
                new SiteSetting { Id = 8, Key = "AboutText",      Value = "Brikon Yapı olarak kaliteli ve güvenilir inşaat hizmetleri sunuyoruz.", Description = "Hakkımızda metni" },
                new SiteSetting { Id = 9,  Key = "WhatsApp",        Value = "",                    Description = "WhatsApp numarası" },
                new SiteSetting { Id = 10, Key = "MarqueeMode",    Value = "image",               Description = "Kayan bant modu" },
                new SiteSetting { Id = 11, Key = "AboutTitle",     Value = "Güvenilir Bir Yapı Ortağı", Description = "Hakkımızda başlık" },
                new SiteSetting { Id = 12, Key = "AboutText2",     Value = "Her projemizde kaliteli malzeme kullanımı, zamanında teslimat ve müşteri memnuniyetini ön planda tutuyoruz. Modern mimarinin estetik anlayışını, işlevsellikle birleştirerek yaşam kalitesini artırıyoruz.", Description = "Hakkımızda 2. paragraf" },
                new SiteSetting { Id = 13, Key = "AboutYearFounded", Value = "2005",              Description = "Kuruluş yılı" },
                new SiteSetting { Id = 14, Key = "AboutYearLabel",  Value = "2005'ten Bu Yana",   Description = "Yıl etiketi" },
                new SiteSetting { Id = 15, Key = "AboutCard1Title", Value = "Kalite Güvencesi",   Description = "Kart 1 başlık" },
                new SiteSetting { Id = 16, Key = "AboutCard1Desc",  Value = "TSE ve ISO standartlarına uygun üretim", Description = "Kart 1 açıklama" },
                new SiteSetting { Id = 17, Key = "AboutCard2Title", Value = "Zamanında Teslimat", Description = "Kart 2 başlık" },
                new SiteSetting { Id = 18, Key = "AboutCard2Desc",  Value = "Söz verilen tarihlerde eksiksiz teslim", Description = "Kart 2 açıklama" },
                new SiteSetting { Id = 19, Key = "AboutCard3Title", Value = "Uzman Kadro",        Description = "Kart 3 başlık" },
                new SiteSetting { Id = 20, Key = "AboutCard3Desc",  Value = "Deneyimli mühendis ve usta ekibi", Description = "Kart 3 açıklama" },
                new SiteSetting { Id = 21, Key = "AboutCard4Title", Value = "Sürdürülebilirlik",  Description = "Kart 4 başlık" },
                new SiteSetting { Id = 22, Key = "AboutCard4Desc",  Value = "Çevre dostu ve enerji verimli yapılar", Description = "Kart 4 açıklama" },
                new SiteSetting { Id = 23, Key = "AboutMediaPath",  Value = "",                    Description = "Hakkımızda görsel/video yolu" }
            );
        }
    }
}
