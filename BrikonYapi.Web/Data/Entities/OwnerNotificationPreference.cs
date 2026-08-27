using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Kat malikinin bildirim tercihleri (Profilim ekranından yönetilir). Owner ile 1:1.</summary>
    public class OwnerNotificationPreference
    {
        public int Id { get; set; }

        [Required] public int OwnerId { get; set; }
        public Owner? Owner { get; set; }

        // ── Bildirim kanalları ────────────────────────────────
        public bool PushEnabled { get; set; } = true;
        public bool SmsEnabled { get; set; } = true;
        public bool EmailEnabled { get; set; } = true;
        public bool WhatsAppEnabled { get; set; } = true;

        // ── Bildirim tipleri ──────────────────────────────────
        public bool NotifyPayment { get; set; } = true;
        public bool NotifyProgress { get; set; } = true;
        public bool NotifyPoll { get; set; } = true;
        public bool NotifyChat { get; set; } = false;
        public bool NotifyNews { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
