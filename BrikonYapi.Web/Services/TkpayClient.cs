using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace BrikonYapi.Web.Services
{
    /// <summary>TKPAY Ödeme Formu (Checkout Form / "commonpay") sonucu.</summary>
    public record TkpayCheckoutResult(bool Success, string? LinkUrl, string? Token, string? Error);

    /// <summary>TKPAY commonpay/inquiry sonucu. TransactionStatus: "Success" | "Pending" | "Error" (TKPAY'in döndüğü haliyle).</summary>
    public record TkpayInquiryResult(
        bool CallSucceeded,
        string? Error,
        string? TransactionStatus,
        string? FormStatus,
        string? OrderId,
        decimal? Amount,
        decimal? BaseAmount,
        string? Currency,
        int? InstallmentCount,
        decimal? InstallmentInterestAmount,
        string? Bin)
    {
        public bool IsSuccess => string.Equals(TransactionStatus, "Success", StringComparison.OrdinalIgnoreCase);
        public bool IsError   => string.Equals(TransactionStatus, "Error", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TKPAY Ödeme Platformu (merchant-api v2) istemcisi — yalnızca Ödeme Formu (hosted checkout) akışı.
    /// Kart bilgisi bu sisteme hiç gelmez: malik TKPAY'in güvenli sayfasında kartını girer.
    ///
    /// Yapılandırma (Coolify ortam değişkenleri, ör. Tkpay__ClientId):
    ///   Tkpay:Enabled, Tkpay:ApiBaseUrl, Tkpay:AuthUrl, Tkpay:ClientId, Tkpay:ClientSecret,
    ///   Tkpay:Username, Tkpay:Password, Tkpay:StoreKey, Tkpay:MerchantId,
    ///   Tkpay:InstallmentInterest (vade farkı müşteriye yansısın, varsayılan true),
    ///   Tkpay:EnabledInstallments (ör. "2,3,6,9,12"; boşsa işyerine tanımlı tümü),
    ///   Tkpay:LinkExpireMinutes (varsayılan 15).
    /// GÜVENLİK: kimlik bilgileri kodda/appsettings'te tutulmaz, yalnızca ortam değişkeninden okunur.
    /// </summary>
    public class TkpayClient
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TkpayClient> _logger;

        private const string TokenCacheKey = "tkpay:access-token";

        public TkpayClient(IConfiguration config, IHttpClientFactory httpFactory, IMemoryCache cache, ILogger<TkpayClient> logger)
        {
            _config = config;
            _httpFactory = httpFactory;
            _cache = cache;
            _logger = logger;
        }

        private string Cfg(string key) => (_config[$"Tkpay:{key}"] ?? "").Trim();

        private string ApiBase => (Cfg("ApiBaseUrl") is { Length: > 0 } u ? u : "https://sandbox-api.tkpay.com").TrimEnd('/');
        private string AuthUrl => Cfg("AuthUrl") is { Length: > 0 } u ? u
            : "https://auth.api.tkpay.com/realms/tkpay-pf-merchant-api/protocol/openid-connect/token";

        /// <summary>Kartla ödeme yalnızca açıkça etkinleştirilmiş ve tüm kimlik bilgileri girilmişse aktiftir.</summary>
        public bool IsConfigured =>
            string.Equals(Cfg("Enabled"), "true", StringComparison.OrdinalIgnoreCase)
            && new[] { "ClientId", "ClientSecret", "Username", "Password", "StoreKey", "MerchantId" }
                .All(k => Cfg(k).Length > 0);

        public bool InstallmentInterest => !string.Equals(Cfg("InstallmentInterest"), "false", StringComparison.OrdinalIgnoreCase);

        public int LinkExpireMinutes => int.TryParse(Cfg("LinkExpireMinutes"), out var m) && m is >= 1 and <= 1440 ? m : 15;

        // ── Yardımcılar ──────────────────────────────────────────────

        /// <summary>TKPAY hash: verilen değerlerin sırayla birleştirilip SHA512 → Base64 alınması.</summary>
        public static string Hash(params string[] parts)
        {
            var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(string.Concat(parts)));
            return Convert.ToBase64String(bytes);
        }

        private static string Rnd() => Convert.ToHexString(RandomNumberGenerator.GetBytes(12)); // 24 karakter
        private static long Ts() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private async Task<string?> GetTokenAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(TokenCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
                return cached;

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = Cfg("Username"),
                ["password"] = Cfg("Password"),
                ["client_id"] = Cfg("ClientId"),
                ["client_secret"] = Cfg("ClientSecret"),
                ["grant_type"] = "password"
            });

            var client = _httpFactory.CreateClient("tkpay");
            using var resp = await client.PostAsync(AuthUrl, form, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Gövde kimlik bilgisi içermez ama yine de yalnızca durum kodunu logluyoruz.
                _logger.LogError("TKPAY token alınamadı: HTTP {Status}", (int)resp.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var token = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            var expires = doc.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var s) ? s : 300;
            if (string.IsNullOrEmpty(token)) return null;

            _cache.Set(TokenCacheKey, token, TimeSpan.FromSeconds(Math.Max(30, expires - 60)));
            return token;
        }

        private async Task<(JsonElement? Data, string? Error)> PostAsync(string path, object payload, CancellationToken ct)
        {
            var token = await GetTokenAsync(ct);
            if (token == null) return (null, "Ödeme sağlayıcısına bağlanılamadı (yetkilendirme).");

            var client = _httpFactory.CreateClient("tkpay");
            using var req = new HttpRequestMessage(HttpMethod.Post, ApiBase + path)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                _cache.Remove(TokenCacheKey); // token süresi dolmuş olabilir; bir sonraki çağrıda yenilenir

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var ok = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
                if (!ok)
                {
                    string? msg = null;
                    if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
                    {
                        var code = err.TryGetProperty("code", out var c) ? c.ToString() : "";
                        msg = (err.TryGetProperty("message", out var m) ? m.GetString() : null) ?? "";
                        _logger.LogWarning("TKPAY {Path} hata: {Code} {Message}", path, code, msg);
                    }
                    return (null, string.IsNullOrWhiteSpace(msg) ? "Ödeme sağlayıcısı isteği reddetti." : msg);
                }
                JsonElement? data = root.TryGetProperty("data", out var d) ? d.Clone() : null;
                return (data, null);
            }
            catch (JsonException)
            {
                _logger.LogError("TKPAY {Path} geçersiz yanıt: HTTP {Status}", path, (int)resp.StatusCode);
                return (null, "Ödeme sağlayıcısından beklenmeyen yanıt alındı.");
            }
        }

        private static string? Str(JsonElement d, string name) =>
            d.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

        private static decimal? Dec(JsonElement d, string name) =>
            decimal.TryParse(Str(d, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var x) ? x : null;

        private static int? Int(JsonElement d, string name) =>
            int.TryParse(Str(d, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : null;

        // ── Ödeme Formu ──────────────────────────────────────────────

        /// <summary>Ödeme formu oturumu oluşturur; malik dönen linkUrl'e yönlendirilir.</summary>
        public async Task<TkpayCheckoutResult> CreateCheckoutAsync(
            string externalId, decimal amount, string currency, string okUrl, string failUrl,
            string? email, string? phone, string? note, string? invoiceNo, CancellationToken ct = default)
        {
            if (!IsConfigured) return new(false, null, null, "Kartla ödeme henüz aktif değil.");

            var rnd = Rnd();
            var ts = Ts();
            var amountStr = Money(amount);
            const string installmentType = "INSTALLMENT"; // tek çekim de bu sayfada seçilebilir
            const string nonSecure = "false";             // 3D Secure zorunlu

            // hash = storeKey + merchantId + username + rnd + ts + externalId + amount + currency
            //        + installmentType + nonSecureEnabled + callbackOkUrl + callbackFailUrl
            var hash = Hash(Cfg("StoreKey"), Cfg("MerchantId"), Cfg("Username"), rnd, ts.ToString(CultureInfo.InvariantCulture),
                            externalId, amountStr, currency, installmentType, nonSecure, okUrl, failUrl);

            var payload = new Dictionary<string, object?>
            {
                ["rnd"] = rnd,
                ["timestamp"] = ts,
                ["hash"] = hash,
                ["callbackOkUrl"] = okUrl,
                ["callbackFailUrl"] = failUrl,
                ["externalId"] = externalId,
                ["amount"] = amountStr,
                ["currency"] = currency,
                ["nonSecureEnabled"] = false,
                ["installmentType"] = installmentType,
                ["installmentInterest"] = InstallmentInterest,
                ["linkExpireMinutes"] = LinkExpireMinutes,
            };
            if (!string.IsNullOrWhiteSpace(invoiceNo)) payload["invoiceOrderNumber"] = invoiceNo;
            if (!string.IsNullOrWhiteSpace(note)) payload["merchantNote"] = note.Length > 500 ? note[..500] : note;
            if (!string.IsNullOrWhiteSpace(email)) payload["email"] = email;
            if (!string.IsNullOrWhiteSpace(phone)) payload["phone"] = phone;

            var enabled = Cfg("EnabledInstallments")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n >= 2 && n <= 36).Distinct().ToList();
            if (enabled.Count > 0) payload["enabledInstallments"] = enabled;

            var (data, error) = await PostAsync("/pf/merchant-api/v2/commonpay", payload, ct);
            if (data == null) return new(false, null, null, error);

            var link = Str(data.Value, "linkUrl") ?? Str(data.Value, "link");
            // GÜVENLİK: malik yalnızca TKPAY alan adına yönlendirilir (açık yönlendirme önlemi).
            Uri? uri = null;
            if (link == null || !Uri.TryCreate(link, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps
                || !(uri.Host.Equals("tkpay.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".tkpay.com", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogError("TKPAY beklenmeyen ödeme linki döndü: {Host}", uri?.Host);
                return new(false, null, null, "Ödeme sayfası oluşturulamadı.");
            }
            return new(true, link, Str(data.Value, "token"), null);
        }

        /// <summary>Ödeme formunun nihai sonucunu sorgular. Redirect/callback tek başına güvenilmez;
        /// sipariş kararı yalnızca bu sorgunun sonucuyla verilir (TKPAY dokümanı 6.2).</summary>
        public async Task<TkpayInquiryResult> InquireCheckoutAsync(string externalId, CancellationToken ct = default)
        {
            if (!IsConfigured) return new(false, "Kartla ödeme aktif değil.", null, null, null, null, null, null, null, null, null);

            var rnd = Rnd();
            var ts = Ts();
            var hash = Hash(Cfg("StoreKey"), Cfg("MerchantId"), Cfg("Username"), rnd, ts.ToString(CultureInfo.InvariantCulture), externalId);
            var (data, error) = await PostAsync("/pf/merchant-api/v2/commonpay/inquiry",
                new { rnd, timestamp = ts, hash, externalId }, ct);
            if (data == null) return new(false, error, null, null, null, null, null, null, null, null, null);

            var d = data.Value;
            return new(true, null,
                Str(d, "transactionStatus"), Str(d, "status"), Str(d, "orderId"),
                Dec(d, "amount"), Dec(d, "baseAmount"), Str(d, "currency"),
                Int(d, "installmentCount"), Dec(d, "installmentInterestAmount"), Str(d, "bin"));
        }
    }
}
