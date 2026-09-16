using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>"Fon Ödemeleri" özelliği: bağımsız bölüme (Unit) bağlı olmayan, proje bazlı taksitli
    /// borç planı sahiplerini (ör. Arsa Sahibi) temsil eden FundDebtors tablosu eklenir.
    /// PaymentSchedules.UnitId nullable yapılır ve yeni nullable FundDebtorId FK'si eklenir —
    /// bir plan ya bir Unit'e ya da bir FundDebtor'a bağlı olur, ikisi birden değil.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260916120000_AddFundDebtors")]
    public partial class AddFundDebtors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FundDebtors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    FundType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundDebtors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundDebtors_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundDebtors_ProjectId",
                table: "FundDebtors",
                column: "ProjectId");

            migrationBuilder.AlterColumn<int>(
                name: "UnitId",
                table: "PaymentSchedules",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "FundDebtorId",
                table: "PaymentSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSchedules_FundDebtorId",
                table: "PaymentSchedules",
                column: "FundDebtorId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSchedules_FundDebtors_FundDebtorId",
                table: "PaymentSchedules",
                column: "FundDebtorId",
                principalTable: "FundDebtors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSchedules_FundDebtors_FundDebtorId",
                table: "PaymentSchedules");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSchedules_FundDebtorId",
                table: "PaymentSchedules");

            migrationBuilder.DropColumn(
                name: "FundDebtorId",
                table: "PaymentSchedules");

            migrationBuilder.AlterColumn<int>(
                name: "UnitId",
                table: "PaymentSchedules",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropTable(name: "FundDebtors");
        }
    }
}
