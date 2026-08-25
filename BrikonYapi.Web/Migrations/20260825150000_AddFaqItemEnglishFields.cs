using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    public partial class AddFaqItemEnglishFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuestionEn",
                table: "Faqs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnswerEn",
                table: "Faqs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "QuestionEn", table: "Faqs");
            migrationBuilder.DropColumn(name: "AnswerEn", table: "Faqs");
        }
    }
}
