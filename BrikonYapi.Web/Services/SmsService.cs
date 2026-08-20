using System.Text;
using System.Text.Json;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// NetGSM REST v2 API üzerinden SMS gönderimi. Hesap bilgileri (appsettings/ortam değişkeni
    /// üzerinden NetGsm:Usercode, NetGsm:Password, NetGsm:Header) boşsa gönderim sessizce atlanır
    /// (EmailService'teki aynı "yapılandırılmamışsa no-op" deseni) — böylece hesap açılana kadar
    /// uygulama hatasız çalışmaya devam eder.
    ///
    /// NOT: Bu entegrasyon NetGSM'in genel REST v2 uç noktası ve JSON gövde biçimine göre
    /// yazılmıştır. Hesap açıldıktan sonra NetGSM panelinden gelen güncel API dokümanıyla
    /// karşılaştırıp doğrulamanız önerilir (bilgibankasi.netgsm.com.tr/sms/toplu-sms/api-ile-sms).
    /// </summary>
    public class SmsService
    {
        private const string ApiUrl = "https://api.netgsm.com.tr/sms/rest/v2/send";

        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<SmsService> _logger;

        public SmsService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<SmsService> logger)
        {
            _config = config;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_config["NetGsm:Usercode"]) &&
            !string.IsNullOrWhiteSpace(_config["NetGsm:Password"]) &&
            !string.IsNullOrWhiteSpace(_config["NetGsm:Header"]);

        /// <summary>Tek bir cep numarasına SMS gönderir. Başarı/hata durumunu döner, exception fırlatmaz
        /// (çağıran taraf — PaymentNotificationService — sonucu NotificationLog'a yazar).</summary>
        public async Task<(bool Success, string? Error)> SendAsync(string phone, string message)
        {
            if (!IsConfigured)
                return (false, "SMS sağlayıcısı yapılandırılmamış.");

            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 10)
                return (false, "Geçersiz telefon numarası.");
            // NetGSM 90 ülke kodsuz, başında 0 olmadan 10 haneli GSM numarası bekler (5xxxxxxxxx).
            if (digits.Length > 10) digits = digits[^10..];

            try
            {
                var usercode = _config["NetGsm:Usercode"]!;
                var password = _config["NetGsm:Password"]!;
                var header = _config["NetGsm:Header"]!;

                var payload = new
                {
                    msgheader = header,
                    encoding = "TR",
                    iysfilter = "",
                    partnercode = "",
                    messages = new[] { new { msg = message, no = digits } }
                };

                var client = _httpFactory.CreateClient("netgsm");
                client.Timeout = TimeSpan.FromSeconds(10);

                var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usercode}:{password}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(ApiUrl, content);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return (true, null);

                _logger.LogWarning("NetGSM SMS gönderilemedi ({Status}): {Body}", response.StatusCode, body);
                return (false, $"SMS sağlayıcı hatası ({(int)response.StatusCode}).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NetGSM SMS gönderimi sırasında hata.");
                return (false, "SMS gönderilemedi.");
            }
        }
    }
}
