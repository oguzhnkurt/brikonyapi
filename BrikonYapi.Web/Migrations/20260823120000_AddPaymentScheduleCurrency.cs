using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>Ödeme kalemlerine para birimi (TL/USD/EUR) alanı eklenir. Bazı projelerde ödeme
    /// dolar veya euro kuru üzerinden alınıyor — malik doğrudan o para biriminde öder, TL karşılığı
    /// hesaplanmaz. Varsayılan TL'dir, mevcut kayıtlar etkilenmez.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260823120000_AddPaymentScheduleCurrency")]
    public partial class AddPaymentScheduleCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "PaymentSchedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Currency", table: "PaymentSchedules");
        }
    }
}
