using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BrikonYapi.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAboutSectionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SiteSettings",
                columns: new[] { "Id", "Description", "Key", "Value" },
                values: new object[,]
                {
                    { 10, "Kayan bant modu", "MarqueeMode", "image" },
                    { 11, "Hakkımızda başlık", "AboutTitle", "Güvenilir Bir Yapı Ortağı" },
                    { 12, "Hakkımızda 2. paragraf", "AboutText2", "Her projemizde kaliteli malzeme kullanımı, zamanında teslimat ve müşteri memnuniyetini ön planda tutuyoruz. Modern mimarinin estetik anlayışını, işlevsellikle birleştirerek yaşam kalitesini artırıyoruz." },
                    { 13, "Kuruluş yılı", "AboutYearFounded", "2005" },
                    { 14, "Yıl etiketi", "AboutYearLabel", "2005'ten Bu Yana" },
                    { 15, "Kart 1 başlık", "AboutCard1Title", "Kalite Güvencesi" },
                    { 16, "Kart 1 açıklama", "AboutCard1Desc", "TSE ve ISO standartlarına uygun üretim" },
                    { 17, "Kart 2 başlık", "AboutCard2Title", "Zamanında Teslimat" },
                    { 18, "Kart 2 açıklama", "AboutCard2Desc", "Söz verilen tarihlerde eksiksiz teslim" },
                    { 19, "Kart 3 başlık", "AboutCard3Title", "Uzman Kadro" },
                    { 20, "Kart 3 açıklama", "AboutCard3Desc", "Deneyimli mühendis ve usta ekibi" },
                    { 21, "Kart 4 başlık", "AboutCard4Title", "Sürdürülebilirlik" },
                    { 22, "Kart 4 açıklama", "AboutCard4Desc", "Çevre dostu ve enerji verimli yapılar" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 22);
        }
    }
}
