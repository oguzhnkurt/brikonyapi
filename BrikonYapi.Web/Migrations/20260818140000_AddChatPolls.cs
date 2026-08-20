using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>
    /// Sohbet anketi: yönetim, bir proje sohbetinde WhatsApp tarzı hızlı anket açabilir
    /// (soru + seçenekler). Anket, sohbet akışında normal bir mesaj gibi görünür ve
    /// malikler seçeneğe dokunarak oy verir/oylarını değiştirir.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818140000_AddChatPolls")]
    public partial class AddChatPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatPolls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatPolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatPolls_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatPollOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatPollId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatPollOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatPollOptions_ChatPolls_ChatPollId",
                        column: x => x.ChatPollId,
                        principalTable: "ChatPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatPollVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatPollId = table.Column<int>(type: "integer", nullable: false),
                    ChatPollOptionId = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatPollVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatPollVotes_ChatPolls_ChatPollId",
                        column: x => x.ChatPollId,
                        principalTable: "ChatPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatPollVotes_ChatPollOptions_ChatPollOptionId",
                        column: x => x.ChatPollOptionId,
                        principalTable: "ChatPollOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatPollVotes_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<bool>(
                name: "IsPoll",
                table: "ChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ChatPollId",
                table: "ChatMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatPolls_ProjectId",
                table: "ChatPolls",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatPollOptions_ChatPollId",
                table: "ChatPollOptions",
                column: "ChatPollId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatPollVotes_ChatPollId_OwnerId",
                table: "ChatPollVotes",
                columns: new[] { "ChatPollId", "OwnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatPollVotes_ChatPollOptionId",
                table: "ChatPollVotes",
                column: "ChatPollOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatPollVotes_OwnerId",
                table: "ChatPollVotes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatPollId",
                table: "ChatMessages",
                column: "ChatPollId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatPolls_ChatPollId",
                table: "ChatMessages",
                column: "ChatPollId",
                principalTable: "ChatPolls",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_ChatMessages_ChatPolls_ChatPollId", table: "ChatMessages");
            migrationBuilder.DropIndex(name: "IX_ChatMessages_ChatPollId", table: "ChatMessages");
            migrationBuilder.DropColumn(name: "ChatPollId", table: "ChatMessages");
            migrationBuilder.DropColumn(name: "IsPoll", table: "ChatMessages");
            migrationBuilder.DropTable(name: "ChatPollVotes");
            migrationBuilder.DropTable(name: "ChatPollOptions");
            migrationBuilder.DropTable(name: "ChatPolls");
        }
    }
}
