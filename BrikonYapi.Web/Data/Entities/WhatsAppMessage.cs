namespace BrikonYapi.Web.Data.Entities
{
    public enum WhatsAppDirection { Inbound = 0, Outbound = 1 }

    /// <summary>
    /// WhatsApp gelen kutusu kaydı: kat malikinin işletme numarasına yazdığı mesajlar (360dialog
    /// webhook'u ile gelir) ve yöneticinin admin panelinden verdiği serbest metin yanıtlar.
    /// Meta kuralı gereği serbest metin yanıt yalnızca kişinin son gelen mesajından sonraki
    /// 24 saat içinde gönderilebilir; bu süre bu tablodaki son Inbound kaydından hesaplanır.
    /// </summary>
    public class WhatsAppMessage
    {
        public int Id { get; set; }

        /// <summary>Karşı tarafın numarası, ülke kodlu ve + işaretsiz (ör. 905xxxxxxxxx).</summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary>Numara bir kat malikiyle eşleşiyorsa o malik (eşleşmezse null).</summary>
        public int? OwnerId { get; set; }
        public Owner? Owner { get; set; }

        /// <summary>WhatsApp profil adı (webhook'taki contacts[].profile.name).</summary>
        public string? ContactName { get; set; }

        public WhatsAppDirection Direction { get; set; }

        /// <summary>text, image, document, audio, video, location, sticker, reaction, unknown ...</summary>
        public string MessageType { get; set; } = "text";

        public string Body { get; set; } = string.Empty;

        /// <summary>WhatsApp mesaj kimliği (wamid...). Durum güncellemelerini eşlemek ve
        /// webhook tekrarlarında çift kaydı önlemek için kullanılır.</summary>
        public string? WaMessageId { get; set; }

        /// <summary>Giden mesajlar için: sent / delivered / read / failed.</summary>
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>Gelen mesaj yönetici tarafından görüldü mü.</summary>
        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Kat maliki telefonunu (05xx..., 5xx..., +90 5xx...) WhatsApp kimliği biçimine
        /// (905xxxxxxxxx) çevirir; webhook'tan gelen "from" değeriyle aynı biçim.</summary>
        public static string ToWaId(string? phone)
        {
            var d = new string((phone ?? "").Where(char.IsDigit).ToArray());
            return d.Length >= 10 ? "90" + d[^10..] : d;
        }

        /// <summary>Şablonla giden bir mesajın gelen kutusundaki sohbet geçmişine eklenecek kaydı.</summary>
        public static WhatsAppMessage OutboundTemplate(string? phone, int ownerId, string text, bool ok, string? error) => new()
        {
            Phone = ToWaId(phone),
            OwnerId = ownerId,
            Direction = WhatsAppDirection.Outbound,
            MessageType = "template",
            Body = text,
            Status = ok ? "sent" : "failed",
            ErrorMessage = error,
            IsRead = true,
            CreatedAt = DateTime.Now
        };
    }
}
