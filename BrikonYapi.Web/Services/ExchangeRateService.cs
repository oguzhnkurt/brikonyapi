using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// TCMB'nin (Türkiye Cumhuriyet Merkez Bankası) günlük USD/TRY satış kurunu çeker ve
    /// bir süre önbellekte tutar. Ödeme ekranlarında TL tutarların yanında yalnızca
    /// bilgi amaçlı "≈ $X" gösterimi için kullanılır.
    ///
    /// ÖNEMLİ: Bu servis hiçbir zaman ödeme yükümlülüğünü dolara çevirmez. 32 sayılı Karar
    /// gereği Türkiye'de yerleşik taraflar arasındaki gayrimenkul sözleşmelerinde bedel döviz
    /// cinsinden belirlenemez — tahsilat ve resmi tutar her zaman TL'dir, dolar karşılığı
    /// sadece malikin fikir edinmesi için gösterilir.
    /// </summary>
    public class ExchangeRateService
    {
        private const string TcmbUrl = "https://www.tcmb.gov.tr/kurlar/today.xml";
        private const string CacheKey = "exchangerate_usdtry";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(IHttpClientFactory httpFactory, IMemoryCache cache, ILogger<ExchangeRateService> logger)
        {
            _httpFactory = httpFactory;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>1 USD kaç TL eder (TCMB döviz satış kuru). Kur alınamazsa null döner —
        /// çağıran taraf bu durumda dolar karşılığını hiç göstermemelidir.</summary>
        public async Task<decimal?> GetUsdToTryAsync()
        {
            if (_cache.TryGetValue<decimal>(CacheKey, out var cached) && cached > 0)
                return cached;

            try
            {
                var client = _httpFactory.CreateClient("tcmb");
                client.Timeout = TimeSpan.FromSeconds(5);
                var xml = await client.GetStringAsync(TcmbUrl);

                var doc = XDocument.Parse(xml);
                var usdNode = doc.Descendants("Currency")
                    .FirstOrDefault(c => (string?)c.Attribute("CurrencyCode") == "USD");

                var rateText = usdNode?.Element("ForexSelling")?.Value;
                if (string.IsNullOrWhiteSpace(rateText))
                    rateText = usdNode?.Element("BanknoteSelling")?.Value;

                if (decimal.TryParse(rateText, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate) && rate > 0)
                {
                    _cache.Set(CacheKey, rate, CacheDuration);
                    return rate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TCMB kur bilgisi alınamadı.");
            }

            return null;
        }

        /// <summary>Bir TL tutarın "≈ $1.234" biçiminde okunabilir dolar karşılığı. Kur alınamazsa null
        /// döner (bu durumda arayan taraf ekranda dolar satırını hiç göstermemelidir).</summary>
        public async Task<string?> FormatUsdEquivalentAsync(decimal tlAmount)
        {
            var rate = await GetUsdToTryAsync();
            if (rate is null or 0) return null;

            var usd = tlAmount / rate.Value;
            return "≈ $" + usd.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
