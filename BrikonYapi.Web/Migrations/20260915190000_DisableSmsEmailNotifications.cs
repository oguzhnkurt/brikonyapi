using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>SMS ve e-posta bildirim kanalları şu an için devre dışı bırakıldı (yalnızca WhatsApp
    /// aktif kalsın istendi). Mevcut tüm OwnerNotificationPreference kayıtlarında SmsEnabled ve
    /// EmailEnabled false'a çekilir; WhatsAppEnabled dokunulmadan kalır. Yeni malikler için de
    /// entity varsayılanları (OwnerNotificationPreference.cs) aynı şekilde false olarak güncellendi,
    /// böylece ileride tercih kaydı ilk kez oluşturulduğunda da SMS/e-posta kapalı başlar.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260915190000_DisableSmsEmailNotifications")]
    public partial class DisableSmsEmailNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"OwnerNotificationPreferences\" SET \"SmsEnabled\" = FALSE, \"EmailEnabled\" = FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"OwnerNotificationPreferences\" SET \"SmsEnabled\" = TRUE, \"EmailEnabled\" = TRUE;");
        }
    }
}
