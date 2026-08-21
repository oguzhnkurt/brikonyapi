using System.Security.Cryptography;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// Kat maliklerine verilecek ilk şifreleri üretir. Şifreler telefonda okunup elle
    /// yazılabilecek kadar sade ("Brikon!4827"), ancak her malik için farklıdır.
    ///
    /// Identity kuralları (bkz. Program.cs): en az 8 karakter, büyük harf ve rakam zorunlu.
    /// Üretilen kalıp bunların hepsini karşılar.
    /// </summary>
    public static class OwnerPasswordGenerator
    {
        // Karışması kolay karakterler (I l 1 O 0) kullanılmıyor; ayraçlar telefonda tarif
        // edilmesi kolay olanlardan seçildi.
        private static readonly char[] Separators = { '!', '*', '.', '?', '-' };

        private const string Prefix = "Brikon";

        /// <summary>Örn. "Brikon!4827", "Brikon.9153".</summary>
        public static string Generate()
        {
            var sep    = Separators[RandomNumberGenerator.GetInt32(Separators.Length)];
            var number = RandomNumberGenerator.GetInt32(1000, 10000); // 4 haneli
            return $"{Prefix}{sep}{number}";
        }
    }
}
