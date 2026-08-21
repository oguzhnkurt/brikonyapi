using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>İnşaat Aşamaları / İş Adımları ekranına SantiyePro tarzı ağırlık, tarih aralığı,
    /// tahmini bütçe ve iş adımı bazında ilerleme yüzdesi alanları eklenir.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260821120000_AddProjectStageWorkItemFields")]
    public partial class AddProjectStageWorkItemFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeightPercentage",
                table: "ProjectStages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercentage",
                table: "ProjectStages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // type parametresi bilinçli olarak belirtilmedi: PostgreSQL (prod) ve SQL Server (yerel)
            // arasında ortak bir DateTime tip adı yok — belirtilmezse EF Core, Migrate() çalıştığı anda
            // hangi sağlayıcı aktifse onun varsayılan tipini kullanır (Npgsql → timestamp, SqlServer → datetime2).
            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedStartDate",
                table: "ProjectStages",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedEndDate",
                table: "ProjectStages",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedBudget",
                table: "ProjectStages",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EstimatedBudget", table: "ProjectStages");
            migrationBuilder.DropColumn(name: "PlannedEndDate", table: "ProjectStages");
            migrationBuilder.DropColumn(name: "PlannedStartDate", table: "ProjectStages");
            migrationBuilder.DropColumn(name: "ProgressPercentage", table: "ProjectStages");
            migrationBuilder.DropColumn(name: "WeightPercentage", table: "ProjectStages");
        }
    }
}
