using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Proje bazlı malikler arası grup sohbetinde bir mesaj.</summary>
    public class ChatMessage
    {
        public int Id { get; set; }

        [Required] public int ProjectId { get; set; }
        public Project? Project { get; set; }

        /// <summary>Mesajı gönderen kat maliki. Yönetim tarafından gönderilen mesajlarda boştur.</summary>
        public int? OwnerId { get; set; }
        public Owner? Owner { get; set; }

        /// <summary>Gönderen Identity kullanıcısı (yetki doğrulaması ve moderasyon için).</summary>
        [Required, MaxLength(450)] public string SenderUserId { get; set; } = string.Empty;

        /// <summary>Gönderim anındaki görünen ad (malik adı değişse bile geçmiş bozulmasın diye saklanır).</summary>
        [Required, MaxLength(150)] public string SenderName { get; set; } = string.Empty;

        /// <summary>Yönetim (Admin) tarafından gönderildiyse mesaj farklı stillenir.</summary>
        public bool IsFromManagement { get; set; } = false;

        [Required, MaxLength(2000)] public string Body { get; set; } = string.Empty;

        /// <summary>Bu mesaj bir sohbet anketiyse true olur; Body o zaman anketin sorusunu taşır
        /// (moderasyon listesinde düz metin olarak görünmesi için), asıl seçenekler ChatPoll'da.</summary>
        public bool IsPoll { get; set; } = false;
        public int? ChatPollId { get; set; }
        public ChatPoll? ChatPoll { get; set; }

        /// <summary>Moderasyon: silinen mesajlar listede gösterilmez, kayıt saklanır.</summary>
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        [MaxLength(450)] public string? DeletedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
