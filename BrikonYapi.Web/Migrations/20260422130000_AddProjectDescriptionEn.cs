using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    public partial class AddProjectDescriptionEn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortDescriptionEn",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "Projects",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShortDescriptionEn", table: "Projects");
            migrationBuilder.DropColumn(name: "DescriptionEn",      table: "Projects");
        }
    }
}
