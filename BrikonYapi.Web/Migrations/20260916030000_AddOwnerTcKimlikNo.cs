using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>Owners.TcKimlikNo kolonunu ekler — Kat Maliki Portalı girişi artık telefon numarası
    /// (kullanıcı adı) + T.C. Kimlik No (şifre) ile yapılacak (bkz. OwnersController,
    /// KatMaliki/AccountController). Bu migration sadece kolonu ekler; mevcut maliklerin TC no'ları
    /// admin panelinden tek tek girilmeli — biz onları bilmiyoruz, otomatik dolduramayız.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260916030000_AddOwnerTcKimlikNo")]
    public partial class AddOwnerTcKimlikNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TcKimlikNo",
                table: "Owners",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TcKimlikNo",
                table: "Owners");
        }
    }
}
