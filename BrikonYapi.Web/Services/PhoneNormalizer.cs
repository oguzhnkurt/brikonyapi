using System.Text.RegularExpressions;

namespace BrikonYapi.Web.Services
{
    /// <summary>Kat Maliki Portalı girişinde telefon numarası artık kullanıcı adı (Identity UserName)
    /// olarak kullanılıyor (bkz. OwnersController, KatMaliki/AccountController). Adminler numarayı
    /// "0505 064 69 41", "+90 505 064 69 41", "5050646941" gibi farklı biçimlerde girebiliyor —
    /// bu sınıf hepsini aynı kanonik biçime (başında tek "0" olan 11 haneli, ör. "05050646941")
    /// indirger ki aynı numara her zaman aynı UserName'e karşılık gelsin.</summary>
    public static class PhoneNormalizer
    {
        /// <summary>Serbest metin bir telefon numarasını "05XXXXXXXXX" biçimine indirger.
        /// Ayrıştırılamıyorsa (rakam sayısı yetersiz/fazla) null döner.</summary>
        public static string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var digits = Regex.Replace(raw, "[^0-9]", "");

            // +90/90 ülke koduyla başlıyorsa at.
            if (digits.Length == 12 && digits.StartsWith("90")) digits = digits[2..];

            // Başında "0" yoksa ekle (10 haneli "5XXXXXXXXX" girilmiş olabilir).
            if (digits.Length == 10 && digits.StartsWith("5")) digits = "0" + digits;

            if (digits.Length != 11 || !digits.StartsWith("0")) return null;

            return digits;
        }

        /// <summary>Bir T.C. Kimlik Numarasının biçimsel olarak geçerli olup olmadığını kontrol eder
        /// (11 hane, sadece rakam, ilk hane 0 olamaz). Resmi checksum algoritması kasıtlı olarak
        /// uygulanmadı — burada asıl amaç veri girişi hatalarını yakalamak, kimlik doğrulamak değil.</summary>
        public static bool IsValidTcKimlikNo(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var digits = raw.Trim();
            return digits.Length == 11 && digits[0] != '0' && digits.All(char.IsDigit);
        }
    }
}
