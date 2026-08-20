using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>
    /// Malik başına proje erişimi: admin artık hangi malikin hangi projenin oylamalarını
    /// görebileceğini ve hangi proje sohbetine katılabileceğini tek tek atayabilir
    /// (bağımsız bölüm sahipliğinden bağımsız olarak).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260817160000_AddOwnerProjectAccess")]
    public partial class AddOwnerProjectAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OwnerProjectAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    CanSeeProject = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CanChat = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerProjectAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnerProjectAccesses_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OwnerProjectAccesses_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OwnerProjectAccesses_OwnerId_ProjectId",
                table: "OwnerProjectAccesses",
                columns: new[] { "OwnerId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnerProjectAccesses_ProjectId",
                table: "OwnerProjectAccesses",
                column: "ProjectId");

            // Geriye dönük uyumluluk: mevcut maliklerin zaten sahip olduğu bağımsız bölümlerin
            // bulunduğu projeler için otomatik erişim kaydı oluşturulur (oylama + sohbet açık).
            // Böylece bu özellik devreye girdiğinde hiçbir mevcut kullanıcı erişimini kaybetmez;
            // admin bundan sonra bu listeyi dilediği gibi daraltabilir/genişletebilir.
            migrationBuilder.Sql(@"
                INSERT INTO ""OwnerProjectAccesses"" (""OwnerId"", ""ProjectId"", ""CanSeeProject"", ""CanChat"", ""CreatedAt"")
                SELECT DISTINCT u.""OwnerId"", u.""ProjectId"", true, true, NOW()
                FROM ""Units"" u
                WHERE u.""OwnerId"" IS NOT NULL AND u.""IsActive"" = true
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OwnerProjectAccesses");
        }
    }
}
