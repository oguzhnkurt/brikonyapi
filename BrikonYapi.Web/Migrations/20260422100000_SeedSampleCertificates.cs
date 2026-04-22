using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    public partial class SeedSampleCertificates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL") return;

            migrationBuilder.Sql(@"
                INSERT INTO ""Certificates"" (""Title"", ""Description"", ""ImagePath"", ""OrderIndex"", ""IsActive"", ""CreatedAt"")
                SELECT t.title, t.desc, NULL, t.ord, TRUE, NOW() FROM (VALUES
                    ('ISO 9001:2015 Kalite Yönetim Sistemi', 'Kalite yönetim sistemi uluslararası standardı sertifikası', 1),
                    ('ISO 14001:2015 Çevre Yönetim Sistemi', 'Çevre yönetim sistemi uluslararası standardı sertifikası', 2),
                    ('TSE Hizmet Yeterlilik Belgesi',          'Türk Standartları Enstitüsü hizmet yeterlilik belgesi',   3)
                ) AS t(title, desc, ord)
                WHERE NOT EXISTS (SELECT 1 FROM ""Certificates"" WHERE ""Title"" = t.title);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL") return;

            migrationBuilder.Sql(@"
                DELETE FROM ""Certificates""
                WHERE ""Title"" IN (
                    'ISO 9001:2015 Kalite Yönetim Sistemi',
                    'ISO 14001:2015 Çevre Yönetim Sistemi',
                    'TSE Hizmet Yeterlilik Belgesi'
                );
            ");
        }
    }
}
