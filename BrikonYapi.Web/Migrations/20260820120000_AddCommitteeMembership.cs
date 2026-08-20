using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>
    /// Temsil Heyeti: admin bir maliki, belirli bir projenin kat malikleri temsil heyeti
    /// üyesi olarak işaretleyebilir. OwnerProjectAccess üzerine eklenen tek sütunlu, geriye
    /// dönük uyumlu bir genişleme (mevcut kayıtlar false ile başlar).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260820120000_AddCommitteeMembership")]
    public partial class AddCommitteeMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCommitteeMember",
                table: "OwnerProjectAccesses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCommitteeMember",
                table: "OwnerProjectAccesses");
        }
    }
}
