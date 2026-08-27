using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>Ödeme hatırlatmalarının WhatsApp üzerinden de gönderilebilmesi için
    /// OwnerNotificationPreferences tablosuna WhatsAppEnabled tercih alanı eklenir. Varsayılan
    /// true'dur (diğer kanallarla aynı davranış) — mevcut malikler için de otomatik açık olur.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260827130000_AddWhatsAppNotifications")]
    public partial class AddWhatsAppNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppEnabled",
                table: "OwnerNotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "WhatsAppEnabled", table: "OwnerNotificationPreferences");
        }
    }
}
