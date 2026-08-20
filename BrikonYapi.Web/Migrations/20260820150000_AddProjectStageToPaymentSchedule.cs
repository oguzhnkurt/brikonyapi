using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>
    /// Hakediş taksitlerini inşaat aşamalarına (ProjectStage) bağlar. Admin bir taksit tanımlarken
    /// isteğe bağlı olarak bir aşama seçebilir; o aşama "Tamamlandı" işaretlendiğinde taksit malikin
    /// ekranında "İlerleme tamamlandı, ödeme bekleniyor" rozetiyle vurgulanır ve bildirim gönderilir.
    /// SetNull kullanılır ki bir aşama silindiğinde bağlı taksit değil, yalnızca bağlantısı kaybolsun.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260820150000_AddProjectStageToPaymentSchedule")]
    public partial class AddProjectStageToPaymentSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectStageId",
                table: "PaymentSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSchedules_ProjectStageId",
                table: "PaymentSchedules",
                column: "ProjectStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSchedules_ProjectStages_ProjectStageId",
                table: "PaymentSchedules",
                column: "ProjectStageId",
                principalTable: "ProjectStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_PaymentSchedules_ProjectStages_ProjectStageId", table: "PaymentSchedules");
            migrationBuilder.DropIndex(name: "IX_PaymentSchedules_ProjectStageId", table: "PaymentSchedules");
            migrationBuilder.DropColumn(name: "ProjectStageId", table: "PaymentSchedules");
        }
    }
}
