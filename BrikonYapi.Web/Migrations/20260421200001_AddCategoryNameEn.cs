using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    public partial class AddCategoryNameEn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Categories");
        }
    }
}
