using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    public enum PollStatus { Active = 0, Closed = 1, Draft = 2 }

    /// <summary>Kat maliklerine sunulan oylama/anket (malzeme seçimi, karar oylaması vb.).</summary>
    public class Poll
    {
        public int Id { get; set; }

        /// <summary>Boş ise tüm projelerdeki maliklere açıktır.</summary>
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;

        [MaxLength(1000)] public string? Description { get; set; }

        /// <summary>Konsept oylaması: her seçenek, birden fazla odanın render görselini ve bir
        /// malzeme kartını içeren hazır bir tasarım paketidir (ör. Konsept A / B / C).</summary>
        public bool IsConcept { get; set; }

        /// <summary>Konsept oylamasında odalar (virgülle ayrılmış, ör. "Salon,Banyo,Mutfak,Dış Cephe").</summary>
        [MaxLength(500)] public string? ConceptRooms { get; set; }

        /// <summary>Tüm konseptlerin oda görselleri aynı kamera açısından mı? İşaretliyse kat maliki
        /// ekranında A/B karşılaştırma sürgüsü gösterilir.</summary>
        public bool SameCameraAngle { get; set; }

        /// <summary>Rozet olarak gösterilen kategori (Dış Cephe, İç Mekan, Peyzaj, Diğer vb.).</summary>
        [MaxLength(60)] public string? Category { get; set; }

        public PollStatus Status { get; set; } = PollStatus.Active;

        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
        public ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
    }
}
