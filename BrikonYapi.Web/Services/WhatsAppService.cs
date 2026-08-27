using System.Text;
using System.Text.Json;

namespace BrikonYapi.Web.Services
{
    /// <summary>
    /// 360dialog'un WhatsApp Cloud API'si (Meta'nın resmi WhatsApp Business Platform'unun bir
    /// Business Solution Provider üzerinden kullanımı) ile onaylı şablon mesajları gönderir.
    /// Hesap bilgileri (appsettings/ortam değişkeni üzerinden WhatsApp:ApiKey) boşsa gönderim
    /// sessizce atlanır (SmsService/EmailService'teki aynı "yapılandırılmamışsa no-op" deseni) —
    /// böylece hesap açılana kadar uygulama hatasız çalışmaya devam eder.
    ///
    /// ÖNEMLİ: WhatsApp Business API, 24 saatlik müşteri hizmeti penceresi dışında (yani işletme
    /// tarafından başlatılan tüm proaktif mesajlarda — ödeme hatırlatmaları dahil) yalnızca
    /// Meta tarafından ÖNCEDEN ONAYLANMIŞ şablon mesajlarına izin verir. Serbest metin gönderilemez.
    /// Şablon, 360dialog Hub panelinden veya /v1/configs/templates uç noktasından "UTILITY"
    /// kategorisinde oluşturulup Meta onayı beklenmelidir (genelde dakikalar-birkaç saat sürer).
    ///
    /// NOT: Bu entegrasyon 360dialog'un güncel (waba-v2) Cloud API dokümanına göre yazılmıştır
    /// (docs.360dialog.com/docs/messaging-api/api-reference/messages). Hesap açıldıktan sonra
    /// güncel dokümanla karşılaştırıp doğrulamanız önerilir.
    /// </summary>
    public class WhatsAppService
    {
        private const string ApiUrl = "https://waba-v2.360dialog.io/messages";

        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<WhatsAppService> logger)
        {
            _config = config;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_config["WhatsApp:ApiKey"]);

        /// <summary>
        /// Onaylı bir WhatsApp şablonunu tek bir cep numarasına gönderir. Tüm gövde parametreleri
        /// düz metin ({{1}}, {{2}}, ... yer tutucularına karşılık gelir) olarak gönderilir — bu,
        /// şablonun Meta'da tam olarak hangi bileşen tipiyle (currency/date_time vb.) onaylandığından
        /// bağımsız çalışır. Başarı/hata durumunu döner, exception fırlatmaz (çağıran taraf —
        /// PaymentNotificationService — sonucu NotificationLog'a yazar).
        /// </summary>
        public async Task<(bool Success, string? Error)> SendTemplateAsync(
            string phone, string templateName, string languageCode, IReadOnlyList<string> bodyParams)
        {
            if (!IsConfigured)
                return (false, "WhatsApp sağlayıcısı yapılandırılmamış.");

            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 10)
                return (false, "Geçersiz telefon numarası.");
            // Owner.Phone başında 0 olmadan 10 haneli GSM numarası olarak saklanır (5xxxxxxxxx).
            // WhatsApp "to" alanı ülke koduyla, +'sız E.164 bekler (905xxxxxxxxx).
            if (digits.Length > 10) digits = digits[^10..];
            var to = "90" + digits;

            try
            {
                var apiKey = _config["WhatsApp:ApiKey"]!;

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to,
                    type = "template",
                    template = new
                    {
                        name = templateName,
                        language = new { code = languageCode },
                        components = new object[]
                        {
                            new
                            {
                                type = "body",
                                parameters = bodyParams.Select(p => new { type = "text", text = p }).ToArray()
                            }
                        }
                    }
                };

                var client = _httpFactory.CreateClient("whatsapp");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Remove("D360-API-KEY");
                client.DefaultRequestHeaders.Add("D360-API-KEY", apiKey);

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(ApiUrl, content);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return (true, null);

                _logger.LogWarning("WhatsApp mesajı gönderilemedi ({Status}): {Body}", response.StatusCode, body);
                return (false, $"WhatsApp sağlayıcı hatası ({(int)response.StatusCode}).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WhatsApp mesajı gönderimi sırasında hata.");
                return (false, "WhatsApp mesajı gönderilemedi.");
            }
        }
    }
}
