using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>SMS ve e-posta bildirim kanalları 20260915190000_DisableSmsEmailNotifications ile
    /// devre dışı bırakıldı; bu tarihten önce oluşmuş NotificationLog kayıtları (Channel = Sms veya
    /// Email) artık kafa karıştırıyor — admin "biz kimseye bir şey göndermedik neden bunlar
    /// gözüküyor" diye sordu (kayıtlar aslında otomatik taksit/hatırlatma bildirimleriydi, çoğu da
    /// sağlayıcı yapılandırılmadığı için zaten "Başarısız" idi). Bu migration Bildirimler ekranındaki
    /// geçmişten bu iki kanala ait tüm kayıtları temizler. WhatsApp kayıtlarına dokunulmaz.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260916013000_CleanupOldSmsEmailNotificationLogs")]
    public partial class CleanupOldSmsEmailNotificationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NotificationChannel: Sms = 0, Email = 1, WhatsApp = 2
            migrationBuilder.Sql(
                "DELETE FROM \"NotificationLogs\" WHERE \"Channel\" IN (0, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Silinen kayıtlar geri getirilemez (Down işlemi kasıtlı olarak no-op'tur).
        }
    }
}
