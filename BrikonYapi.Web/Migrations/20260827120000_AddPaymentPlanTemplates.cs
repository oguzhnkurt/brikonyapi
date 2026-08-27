using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>"Ödeme Planı Şablonu" özelliği: Units tablosuna finansal alanlar (m² birim fiyat,
    /// hibe/kredi, toplam ödeme tutarı) ve proje bazlı, gruba toplu atanabilir ödeme planı
    /// şablonları (PaymentPlanTemplates/Items) eklenir. Hakediş/aşama bazlı ve takvim/aylık bazlı
    /// olmak üzere iki plan türünü destekler.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260827120000_AddPaymentPlanTemplates")]
    public partial class AddPaymentPlanTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceM2",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubsidyAmount",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractAmount",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentPlanTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PlanType = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlanTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlanTemplates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanTemplates_ProjectId",
                table: "PaymentPlanTemplates",
                column: "ProjectId");

            migrationBuilder.CreateTable(
                name: "PaymentPlanTemplateItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentPlanTemplateId = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ProjectStageId = table.Column<int>(type: "integer", nullable: true),
                    MonthOffset = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlanTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlanTemplateItems_PaymentPlanTemplates_PaymentPlanTemplateId",
                        column: x => x.PaymentPlanTemplateId,
                        principalTable: "PaymentPlanTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentPlanTemplateItems_ProjectStages_ProjectStageId",
                        column: x => x.ProjectStageId,
                        principalTable: "ProjectStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanTemplateItems_PaymentPlanTemplateId_OrderIndex",
                table: "PaymentPlanTemplateItems",
                columns: new[] { "PaymentPlanTemplateId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanTemplateItems_ProjectStageId",
                table: "PaymentPlanTemplateItems",
                column: "ProjectStageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PaymentPlanTemplateItems");
            migrationBuilder.DropTable(name: "PaymentPlanTemplates");
            migrationBuilder.DropColumn(name: "ContractAmount", table: "Units");
            migrationBuilder.DropColumn(name: "SubsidyAmount", table: "Units");
            migrationBuilder.DropColumn(name: "UnitPriceM2", table: "Units");
        }
    }
}
