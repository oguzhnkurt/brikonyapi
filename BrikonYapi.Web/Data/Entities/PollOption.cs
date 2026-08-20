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

        public ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
    }
}
