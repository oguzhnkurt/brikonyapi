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
        // SMS ve e-posta şu an için devre dışı bırakıldı (yalnızca WhatsApp aktif) — bkz. NotificationsController/Owners modalı.
        public bool SmsEnabled { get; set; } = false;
        public bool EmailEnabled { get; set; } = false;
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
