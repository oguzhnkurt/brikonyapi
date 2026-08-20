using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>Duyurulara haber kartı alanları eklenir (kapak görseli, özet, kaynak, ana sayfada göster).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260806170000_AddNewsFieldsToAnnouncement")]
    public partial class AddNewsFieldsToAnnouncement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImagePath",
                table: "Announcements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Announcements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Announcements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnHome",
                table: "Announcements",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShowOnHome", table: "Announcements");
            migrationBuilder.DropColumn(name: "Source", table: "Announcements");
            migrationBuilder.DropColumn(name: "Summary", table: "Announcements");
            migrationBuilder.DropColumn(name: "CoverImagePath", table: "Announcements");
        }
    }
}
