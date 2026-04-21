using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    public partial class AddMissingTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sadece PostgreSQL production için — local SQL Server'da tablolar zaten mevcut
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL") return;

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Certificates"" (
                    ""Id""          SERIAL PRIMARY KEY,
                    ""Title""       TEXT NOT NULL,
                    ""Description"" TEXT,
                    ""ImagePath""   TEXT,
                    ""OrderIndex""  INTEGER NOT NULL DEFAULT 0,
                    ""IsActive""    BOOLEAN NOT NULL DEFAULT TRUE,
                    ""CreatedAt""   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS ""Catalogs"" (
                    ""Id""          SERIAL PRIMARY KEY,
                    ""Title""       TEXT NOT NULL,
                    ""Description"" TEXT,
                    ""Type""        INTEGER NOT NULL DEFAULT 0,
                    ""PdfPath""     TEXT,
                    ""IsActive""    BOOLEAN NOT NULL DEFAULT FALSE,
                    ""CreatedAt""   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""UpdatedAt""   TIMESTAMP WITH TIME ZONE
                );

                INSERT INTO ""SiteSettings"" (""Key"", ""Value"", ""Description"")
                SELECT k.skey, k.sval, k.sdesc FROM (VALUES
                    ('MarqueeMode',    'image',                                  'Kayan bant modu'),
                    ('AboutTitle',     'Güvenilir Bir Yapı Ortağı',              'Hakkımızda başlık'),
                    ('AboutText2',     'Her projemizde kaliteli malzeme kullanımı, zamanında teslimat ve müşteri memnuniyetini ön planda tutuyoruz.',  'Hakkımızda 2. paragraf'),
                    ('AboutYearFounded','2005',                                  'Kuruluş yılı'),
                    ('AboutYearLabel', '2005''ten Bu Yana',                      'Yıl etiketi'),
                    ('AboutCard1Title','Kalite Güvencesi',                       'Kart 1 başlık'),
                    ('AboutCard1Desc', 'TSE ve ISO standartlarına uygun üretim', 'Kart 1 açıklama'),
                    ('AboutCard2Title','Zamanında Teslimat',                     'Kart 2 başlık'),
                    ('AboutCard2Desc', 'Söz verilen tarihlerde eksiksiz teslim', 'Kart 2 açıklama'),
                    ('AboutCard3Title','Uzman Kadro',                            'Kart 3 başlık'),
                    ('AboutCard3Desc', 'Deneyimli mühendis ve usta ekibi',       'Kart 3 açıklama'),
                    ('AboutCard4Title','Sürdürülebilirlik',                      'Kart 4 başlık'),
                    ('AboutCard4Desc', 'Çevre dostu ve enerji verimli yapılar',  'Kart 4 açıklama'),
                    ('AboutMediaPath', '',                                        'Hakkımızda görsel/video yolu')
                ) AS k(skey, sval, sdesc)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""SiteSettings"" WHERE ""Key"" = k.skey
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL") return;

            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS ""Certificates"";
                DROP TABLE IF EXISTS ""Catalogs"";
                DELETE FROM ""SiteSettings"" WHERE ""Key"" IN (
                    'MarqueeMode','AboutTitle','AboutText2','AboutYearFounded','AboutYearLabel',
                    'AboutCard1Title','AboutCard1Desc','AboutCard2Title','AboutCard2Desc',
                    'AboutCard3Title','AboutCard3Desc','AboutCard4Title','AboutCard4Desc','AboutMediaPath'
                );
            ");
        }
    }
}
