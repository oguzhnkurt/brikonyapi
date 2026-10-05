using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Konsept oylamasında bir seçeneğin (konseptin) belirli bir odaya ait render görseli.</summary>
    public class PollOptionImage
    {
        public int Id { get; set; }

        [Required] public int PollOptionId { get; set; }
        public PollOption? PollOption { get; set; }

        /// <summary>Oda adı (Poll.ConceptRooms içindeki adlardan biri).</summary>
        [Required, MaxLength(60)] public string Room { get; set; } = string.Empty;

        [Required, MaxLength(500)] public string ImagePath { get; set; } = string.Empty;
    }
}
