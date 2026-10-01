using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Controllers
{
    /// <summary>
    /// 360dialog (WhatsApp Cloud API) webhook'u. Kat malikleri işletme numarasına yazdığında gelen
    /// mesajlar ve giden mesajların durum güncellemeleri (sent/delivered/read/failed) buraya gelir.
    ///
    /// Güvenlik: 360dialog Hub'da webhook tanımlanırken "X-Webhook-Token" başlığı eklenir. Beklenen
    /// değer WhatsApp:WebhookToken ayarıdır; ayar yoksa WhatsApp:ApiKey'in SHA-256 özetinin ilk 32
    /// karakteri kullanılır (böylece ek bir sunucu ayarı gerekmez, değer yine de gizlidir).
    /// </summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class WhatsAppWebhookController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<WhatsAppWebhookController> _logger;

        public WhatsAppWebhookController(AppDbContext db, IConfiguration config, ILogger<WhatsAppWebhookController> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        /// <summary>Webhook'un beklediği gizli başlık değeri.</summary>
        public static string? ExpectedToken(IConfiguration config)
        {
            var explicitToken = config["WhatsApp:WebhookToken"];
            if (!string.IsNullOrWhiteSpace(explicitToken)) return explicitToken.Trim();

            var apiKey = config["WhatsApp:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes("brikon-webhook:" + apiKey.Trim()));
            return Convert.ToHexString(hash)[..32].ToLowerInvariant();
        }

        [HttpGet("webhooks/whatsapp")]
        public IActionResult Ping() => Ok("ok");

        [HttpPost("webhooks/whatsapp")]
        public async Task<IActionResult> Receive()
        {
            var expected = ExpectedToken(_config);
            if (expected == null) return StatusCode(503);

            var provided = Request.Headers["X-Webhook-Token"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected)))
            {
                _logger.LogWarning("WhatsApp webhook: geçersiz token ile istek reddedildi.");
                return Unauthorized();
            }

            string raw;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                raw = await reader.ReadToEndAsync();

            try
            {
                using var doc = JsonDocument.Parse(raw);
                await ProcessAsync(doc.RootElement);
            }
            catch (Exception ex)
            {
                // Hatalı/beklenmeyen bir yük yüzünden 360dialog'un aynı bildirimi günlerce
                // tekrar denemesini istemiyoruz: logla ve yine de 200 dön.
                _logger.LogError(ex, "WhatsApp webhook işlenemedi. Yük: {Raw}", raw.Length > 2000 ? raw[..2000] : raw);
            }

            return Ok();
        }

        private async Task ProcessAsync(JsonElement root)
        {
            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) return;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;

                    var names = new Dictionary<string, string>();
                    if (value.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == JsonValueKind.Array)
                        foreach (var c in contacts.EnumerateArray())
                        {
                            var waId = Str(c, "wa_id");
                            var name = c.TryGetProperty("profile", out var prof) ? Str(prof, "name") : null;
                            if (waId != null && name != null) names[waId] = name;
                        }

                    if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                        foreach (var m in messages.EnumerateArray())
                            await SaveInboundAsync(m, names);

                    if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                        foreach (var s in statuses.EnumerateArray())
                            await ApplyStatusAsync(s);
                }
            }

            await _db.SaveChangesAsync();
        }

        private async Task SaveInboundAsync(JsonElement m, Dictionary<string, string> names)
        {
            var from = Str(m, "from");
            var id = Str(m, "id");
            if (string.IsNullOrEmpty(from)) return;

            // 360dialog aynı bildirimi tekrar gönderebilir; aynı wamid ikinci kez kaydedilmez.
            if (id != null && await _db.WhatsAppMessages.AnyAsync(x => x.WaMessageId == id)) return;

            var type = Str(m, "type") ?? "unknown";
            var body = type switch
            {
                "text" => m.TryGetProperty("text", out var t) ? Str(t, "body") : null,
                "button" => m.TryGetProperty("button", out var b) ? Str(b, "text") : null,
                "interactive" => InteractiveTitle(m),
                "reaction" => m.TryGetProperty("reaction", out var r) ? $"[Tepki: {Str(r, "emoji")}]" : null,
                "image" => Caption(m, "image", "[Fotoğraf]"),
                "video" => Caption(m, "video", "[Video]"),
                "document" => Caption(m, "document", "[Belge]"),
                "audio" => "[Sesli mesaj]",
                "sticker" => "[Çıkartma]",
                "location" => "[Konum paylaşıldı]",
                "contacts" => "[Kişi kartı paylaşıldı]",
                "system" => m.TryGetProperty("system", out var sys) ? Str(sys, "body") : null,
                _ => "[Desteklenmeyen mesaj türü]"
            } ?? "";

            var timestamp = DateTime.Now;
            if (long.TryParse(Str(m, "timestamp"), out var unix))
                timestamp = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().DateTime;

            _db.WhatsAppMessages.Add(new WhatsAppMessage
            {
                Phone = new string(from.Where(char.IsDigit).ToArray()),
                OwnerId = await FindOwnerIdAsync(from),
                ContactName = names.TryGetValue(from, out var n) ? Trunc(n, 150) : null,
                Direction = WhatsAppDirection.Inbound,
                MessageType = Trunc(type, 30)!,
                Body = body,
                WaMessageId = Trunc(id, 200),
                IsRead = false,
                CreatedAt = timestamp
            });
        }

        private async Task ApplyStatusAsync(JsonElement s)
        {
            var id = Str(s, "id");
            var status = Str(s, "status");
            if (id == null || status == null) return;

            var msg = await _db.WhatsAppMessages.FirstOrDefaultAsync(x => x.WaMessageId == id && x.Direction == WhatsAppDirection.Outbound);
            if (msg == null) return;

            // "read" gelmişse sonradan gelen eski "delivered" üzerine yazmasın.
            int Rank(string? st) => st switch { "sent" => 1, "delivered" => 2, "read" => 3, "failed" => 4, _ => 0 };
            if (Rank(status) >= Rank(msg.Status)) msg.Status = Trunc(status, 20);

            if (status == "failed" && s.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0)
                msg.ErrorMessage = Str(errs[0], "title") ?? Str(errs[0], "message");
        }

        private async Task<int?> FindOwnerIdAsync(string waId)
        {
            var digits = new string(waId.Where(char.IsDigit).ToArray());
            if (digits.Length < 10) return null;
            var last10 = digits[^10..];

            var owners = await _db.Owners.Where(o => o.Phone != null).Select(o => new { o.Id, o.Phone }).ToListAsync();
            return owners.FirstOrDefault(o =>
            {
                var d = new string(o.Phone!.Where(char.IsDigit).ToArray());
                return d.Length >= 10 && d[^10..] == last10;
            })?.Id;
        }

        private static string? InteractiveTitle(JsonElement m)
        {
            if (!m.TryGetProperty("interactive", out var i)) return null;
            if (i.TryGetProperty("button_reply", out var br)) return Str(br, "title");
            if (i.TryGetProperty("list_reply", out var lr)) return Str(lr, "title");
            return "[Etkileşimli yanıt]";
        }

        private static string Caption(JsonElement m, string key, string label)
        {
            if (m.TryGetProperty(key, out var media))
            {
                var cap = Str(media, "caption");
                if (!string.IsNullOrWhiteSpace(cap)) return $"{label} {cap}";
            }
            return label;
        }

        private static string? Str(JsonElement e, string prop) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v)
                ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
                : null;

        private static string? Trunc(string? s, int max) => s == null ? null : (s.Length <= max ? s : s[..max]);
    }
}
