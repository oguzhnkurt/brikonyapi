using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>TKPAY kredi kartı ödemesi: PaymentTransactions tablosuna sipariş no (ExternalId, tekil),
    /// çekilen toplam tutar, taksit sayısı, vade farkı ve kart BIN alanları.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015100000_AddCardPaymentFields")]
    public partial class AddCardPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ExternalId", table: "PaymentTransactions", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ChargedAmount", table: "PaymentTransactions", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "InstallmentInterestAmount", table: "PaymentTransactions", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<int>(name: "InstallmentCount", table: "PaymentTransactions", type: "integer", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CardBin", table: "PaymentTransactions", type: "character varying(8)", maxLength: 8, nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ExternalId",
                table: "PaymentTransactions",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_PaymentTransactions_ExternalId", table: "PaymentTransactions");
            migrationBuilder.DropColumn(name: "ExternalId", table: "PaymentTransactions");
            migrationBuilder.DropColumn(name: "ChargedAmount", table: "PaymentTransactions");
            migrationBuilder.DropColumn(name: "InstallmentInterestAmount", table: "PaymentTransactions");
            migrationBuilder.DropColumn(name: "InstallmentCount", table: "PaymentTransactions");
            migrationBuilder.DropColumn(name: "CardBin", table: "PaymentTransactions");
        }
    }
}
