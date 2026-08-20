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
