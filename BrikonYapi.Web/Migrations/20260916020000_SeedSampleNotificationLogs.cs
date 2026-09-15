using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>Bildirimler ekranındaki "Gönderim Geçmişi" tablosunu boş göstermemek için, mevcut
    /// kat maliklerinden birkaçına atıfla 8 adet örnek NotificationLog kaydı ekler. Bunlar gerçek
    /// gönderim değildir — hem Subject hem de tabloda görünen Mesaj sütunu "(Test örnek)" ile
    /// başlar ki admin panelinde gerçek gönderimlerle karışmasın. İstenirse Subject'e göre
    /// filtrelenip silinebilir.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260916020000_SeedSampleNotificationLogs")]
    public partial class SeedSampleNotificationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NotificationChannel: Sms = 0, Email = 1, WhatsApp = 2
            // NotificationStatus: Sent = 0, Failed = 1, Pending = 2
            migrationBuilder.Sql(@"
                WITH numbered_owners AS (
                    SELECT ""Id"", ROW_NUMBER() OVER (ORDER BY ""Id"") AS rn
                    FROM ""Owners""
                ),
                samples AS (
                    SELECT 1 AS rn, 2 AS channel, '[TEST ÖRNEK] Yeni Ödeme Kalemi — Brikon Yapı' AS subject,
                           '(Test örnek) Taksit 1/12 için ₺250.000 tutarında yeni bir ödeme kalemi tanımlandı. Vade: 27.10.2026.' AS message,
                           0 AS status
                    UNION ALL
                    SELECT 2, 2, '[TEST ÖRNEK] Ödeme Hatırlatması (7 gün kala) — Brikon Yapı',
                           '(Test örnek) Taksit taksitinizin vadesi 1 hafta içinde (23.09.2026) doluyor. Tutar: ₺250.000.', 0
                    UNION ALL
                    SELECT 3, 2, '[TEST ÖRNEK] Ödemeniz Onaylandı — Brikon Yapı',
                           '(Test örnek) Taksit 2/12 (₺250.000) ödemeniz onaylandı. Teşekkür ederiz.', 0
                    UNION ALL
                    SELECT 4, 0, '[TEST ÖRNEK] Ödeme Hatırlatması (1 gün kala) — Brikon Yapı',
                           '(Test örnek) Taksit taksitinizin vadesi yarın (17.09.2026) doluyor. Tutar: ₺250.000.', 1
                    UNION ALL
                    SELECT 5, 1, '[TEST ÖRNEK] Gecikmiş Ödeme — Brikon Yapı',
                           '(Test örnek) Taksit taksitinizin vadesi geçti (10.09.2026). Tutar: ₺250.000. Lütfen en kısa sürede ödeme yapın.', 1
                    UNION ALL
                    SELECT 6, 2, '[TEST ÖRNEK] Ödeme Bildirimi Onaylanmadı — Brikon Yapı',
                           '(Test örnek) Taksit için gönderdiğiniz ödeme bildirimi onaylanmadı. Sebep: Dekont tutarı uyuşmuyor. Lütfen tekrar deneyin veya yönetimle iletişime geçin.', 1
                    UNION ALL
                    SELECT 1, 2, '[TEST ÖRNEK] İlerleme Tamamlandı — Brikon Yapı',
                           '(Test örnek) ""Karkas"" aşaması tamamlandı. Hakediş taksitiniz (₺250.000) ödemeye hazır.', 0
                    UNION ALL
                    SELECT 2, 1, '[TEST ÖRNEK] Yeni Ödeme Kalemi — Brikon Yapı',
                           '(Test örnek) Taksit 3/12 için ₺250.000 tutarında yeni bir ödeme kalemi tanımlandı. Vade: 27.11.2026.', 1
                )
                INSERT INTO ""NotificationLogs"" (""OwnerId"", ""RelatedPaymentScheduleId"", ""Channel"", ""Subject"", ""Message"", ""Status"", ""ErrorMessage"", ""SentAt"", ""CreatedAt"")
                SELECT no.""Id"", NULL, s.channel, s.subject, s.message, s.status,
                       CASE WHEN s.status = 1 THEN 'Test örnek — sağlayıcı yapılandırılmamış' ELSE NULL END,
                       CASE WHEN s.status = 0 THEN NOW() - (s.rn || ' hours')::interval ELSE NULL END,
                       NOW() - (s.rn || ' hours')::interval
                FROM samples s
                JOIN numbered_owners no ON no.rn = ((s.rn - 1) % (SELECT COUNT(*) FROM numbered_owners)) + 1
                WHERE (SELECT COUNT(*) FROM numbered_owners) > 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM \"NotificationLogs\" WHERE \"Subject\" LIKE '[TEST ÖRNEK]%' OR \"Message\" LIKE '(Test örnek)%';");
        }
    }
}
