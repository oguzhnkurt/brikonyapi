using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>Konsept oylaması: Poll.IsConcept/ConceptRooms/SameCameraAngle, PollOption.Description,
    /// oda bazlı render görselleri (PollOptionImages) ve malzeme kartı (PollOptionMaterials).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261005100000_AddConceptPolls")]
    public partial class AddConceptPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "IsConcept", table: "Polls", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "ConceptRooms", table: "Polls", type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "SameCameraAngle", table: "Polls", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "Description", table: "PollOptions", type: "character varying(500)", maxLength: 500, nullable: true);

            migrationBuilder.CreateTable(
                name: "PollOptionImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PollOptionId = table.Column<int>(type: "integer", nullable: false),
                    Room = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollOptionImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollOptionImages_PollOptions_PollOptionId",
                        column: x => x.PollOptionId,
                        principalTable: "PollOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollOptionMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PollOptionId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ColorHex = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    SwatchPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollOptionMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollOptionMaterials_PollOptions_PollOptionId",
                        column: x => x.PollOptionId,
                        principalTable: "PollOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_PollOptionImages_PollOptionId", table: "PollOptionImages", column: "PollOptionId");
            migrationBuilder.CreateIndex(name: "IX_PollOptionMaterials_PollOptionId", table: "PollOptionMaterials", column: "PollOptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PollOptionImages");
            migrationBuilder.DropTable(name: "PollOptionMaterials");
            migrationBuilder.DropColumn(name: "IsConcept", table: "Polls");
            migrationBuilder.DropColumn(name: "ConceptRooms", table: "Polls");
            migrationBuilder.DropColumn(name: "SameCameraAngle", table: "Polls");
            migrationBuilder.DropColumn(name: "Description", table: "PollOptions");
        }
    }
}
