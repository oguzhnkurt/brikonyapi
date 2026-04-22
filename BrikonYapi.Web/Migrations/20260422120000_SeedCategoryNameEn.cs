using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    public partial class SeedCategoryNameEn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL") return;

            migrationBuilder.Sql(@"
                UPDATE ""Categories"" SET ""NameEn"" = 'Campus - Educational Buildings' WHERE ""Name"" = 'Kampüs - Eğitim Yapıları'  AND (""NameEn"" IS NULL OR ""NameEn"" = '');
                UPDATE ""Categories"" SET ""NameEn"" = 'Residential - Offices'          WHERE ""Name"" = 'Konutlar - Ofisler'         AND (""NameEn"" IS NULL OR ""NameEn"" = '');
                UPDATE ""Categories"" SET ""NameEn"" = 'Healthcare Buildings'            WHERE ""Name"" = 'Sağlık Yapıları'            AND (""NameEn"" IS NULL OR ""NameEn"" = '');
                UPDATE ""Categories"" SET ""NameEn"" = 'Industrial Buildings'            WHERE ""Name"" = 'Endüstriyel Yapılar'        AND (""NameEn"" IS NULL OR ""NameEn"" = '');
                UPDATE ""Categories"" SET ""NameEn"" = 'Infrastructure - Roads'          WHERE ""Name"" = 'Altyapı - Yol'              AND (""NameEn"" IS NULL OR ""NameEn"" = '');
                UPDATE ""Categories"" SET ""NameEn"" = 'Renovations'                     WHERE ""Name"" = 'Renovasyonlar'              AND (""NameEn"" IS NULL OR ""NameEn"" = '');
                UPDATE ""Categories"" SET ""NameEn"" = 'Embassies'                       WHERE ""Name"" = 'Büyükelçilikler'            AND (""NameEn"" IS NULL OR ""NameEn"" = '');
                UPDATE ""Categories"" SET ""NameEn"" = 'Administrative Buildings'        WHERE ""Name"" = 'İdari Binalar'              AND (""NameEn"" IS NULL OR ""NameEn"" = '');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL") return;

            migrationBuilder.Sql(@"UPDATE ""Categories"" SET ""NameEn"" = NULL;");
        }
    }
}
