using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BrikonYapi.Web.Data;

#nullable disable

namespace BrikonYapi.Web.Migrations
{
    /// <summary>Mevcut kat maliklerinin giriş kullanıcı adını (AspNetUsers.UserName) e-postadan
    /// normalize edilmiş telefon numarasına geçirir (bkz. PhoneNormalizer.cs — bu SQL, o sınıfın
    /// mantığının bir alt kümesini yeniden uygular: boşluk/parantez/tire temizleme, +90/90/090
    /// önekini kaldırma, 5 ile başlayan 10 haneli numaraların başına 0 ekleme).
    ///
    /// ÖNEMLİ: Şifreler burada değiştirilmez. T.C. Kimlik No mevcut maliklerde bilinmediği için
    /// (yeni eklenen Owners.TcKimlikNo sütunu hepsinde NULL) şifreleri eski (üretilmiş) haliyle
    /// kalır. Bir malik gerçekten telefon+TC ile giriş yapabilsin diye, yönetici o malikin
    /// Düzenle ekranından T.C. Kimlik No'sunu girip kaydetmelidir — bu işlem OwnersController.Edit
    /// içinde otomatik olarak şifreyi de TC No'ya sıfırlar.
    ///
    /// Güvenlik için: telefon numarası normalize edilemeyen, birden fazla malikte aynı sonucu
    /// veren (çakışma) veya zaten başka bir kullanıcı tarafından kullanılan numaralar bu göçte
    /// ATLANIR — bunlar admin tarafından Owners/Edit üzerinden elle düzeltilmelidir.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260916040000_MigrateOwnerUsernamesToPhone")]
    public partial class MigrateOwnerUsernamesToPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                WITH candidates AS (
                    SELECT
                        o.""Id"" AS owner_id,
                        o.""UserId"" AS user_id,
                        regexp_replace(o.""Phone"", '[^0-9]', '', 'g') AS digits
                    FROM ""Owners"" o
                    WHERE o.""Phone"" IS NOT NULL AND btrim(o.""Phone"") <> '' AND o.""UserId"" IS NOT NULL
                ),
                normalized AS (
                    SELECT
                        owner_id,
                        user_id,
                        CASE
                            WHEN length(digits) = 11 AND left(digits, 1) = '0' THEN digits
                            WHEN length(digits) = 12 AND left(digits, 2) = '90' THEN '0' || substring(digits from 3)
                            WHEN length(digits) = 13 AND left(digits, 3) = '090' THEN substring(digits from 3)
                            WHEN length(digits) = 10 AND left(digits, 1) = '5' THEN '0' || digits
                            ELSE NULL
                        END AS norm_phone
                    FROM candidates
                ),
                valid AS (
                    SELECT owner_id, user_id, norm_phone
                    FROM normalized
                    WHERE norm_phone IS NOT NULL
                      AND length(norm_phone) = 11
                      AND left(norm_phone, 1) = '0'
                ),
                unique_phones AS (
                    -- Aynı normalize telefona birden fazla malik denk geliyorsa hiçbirine
                    -- dokunma; admin bu çakışmayı Owners/Edit üzerinden elle çözsün.
                    SELECT norm_phone
                    FROM valid
                    GROUP BY norm_phone
                    HAVING COUNT(*) = 1
                ),
                safe AS (
                    SELECT v.owner_id, v.user_id, v.norm_phone
                    FROM valid v
                    INNER JOIN unique_phones up ON up.norm_phone = v.norm_phone
                    -- Bu numara zaten başka bir kullanıcının UserName'i ise atla.
                    WHERE NOT EXISTS (
                        SELECT 1 FROM ""AspNetUsers"" au
                        WHERE au.""NormalizedUserName"" = UPPER(v.norm_phone)
                          AND au.""Id"" <> v.user_id
                    )
                )
                UPDATE ""AspNetUsers"" au
                SET ""UserName"" = safe.norm_phone,
                    ""NormalizedUserName"" = UPPER(safe.norm_phone)
                FROM safe
                WHERE au.""Id"" = safe.user_id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski kullanıcı adları (e-postalar) burada saklanmadığı için geri alınamaz.
            // Gerekirse Owners.Email sütunundan elle eşleştirme yapılabilir.
        }
    }
}
