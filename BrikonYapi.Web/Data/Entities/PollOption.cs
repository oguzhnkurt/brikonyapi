using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Bir oylamanın seçeneği (isteğe bağlı görselli).</summary>
    public class PollOption
    {
        public int Id { get; set; }

        [Required] public int PollId { get; set; }
        public Poll? Poll { get; set; }

        [Required, MaxLength(200)] public string Text { get; set; } = string.Empty;

        [MaxLength(500)] public string? ImagePath { get; set; }

        public int OrderIndex { get; set; } = 0;

        /// <summary>Konsept oylamasında seçeneğin kısa açıklaması (ör. "Doğal tonlar, meşe parke").</summary>
        [MaxLength(500)] public string? Description { get; set; }

        /// <summary>Konsept oylamasında oda bazlı render görselleri.</summary>
        public ICollection<PollOptionImage> Images { get; set; } = new List<PollOptionImage>();

        /// <summary>Konsept oylamasında malzeme kartı (parke, fayans, duvar boyası, dış cephe...).</summary>
        public ICollection<PollOptionMaterial> Materials { get; set; } = new List<PollOptionMaterial>();

        public ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
    }
}
