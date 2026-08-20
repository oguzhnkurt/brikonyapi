using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>Bağımsız bölümlere kat, oda düzeni ve metrekare alanları eklenir
    /// (Kat Maliki Ana Sayfa kartında "3. Kat · 3+1 · 145 m²" gösterimi için).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260817180000_AddUnitFloorRoomArea")]
    public partial class AddUnitFloorRoomArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FloorNo",
                table: "Units",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomLayout",
                table: "Units",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AreaM2",
                table: "Units",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AreaM2", table: "Units");
            migrationBuilder.DropColumn(name: "RoomLayout", table: "Units");
            migrationBuilder.DropColumn(name: "FloorNo", table: "Units");
        }
    }
}
