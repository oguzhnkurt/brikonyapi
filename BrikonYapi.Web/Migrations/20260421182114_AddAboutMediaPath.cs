using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAboutMediaPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SiteSettings",
                columns: new[] { "Id", "Description", "Key", "Value" },
                values: new object[] { 23, "Hakkımızda görsel/video yolu", "AboutMediaPath", "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 23);
        }
    }
}
