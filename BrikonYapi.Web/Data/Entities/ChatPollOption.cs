using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Bir sohbet anketinin seçeneği.</summary>
    public class ChatPollOption
    {
        public int Id { get; set; }

        [Required] public int ChatPollId { get; set; }
        public ChatPoll? ChatPoll { get; set; }

        [Required, MaxLength(200)] public string Text { get; set; } = string.Empty;

        public int OrderIndex { get; set; } = 0;

        public ICollection<ChatPollVote> Votes { get; set; } = new List<ChatPollVote>();
    }
}
