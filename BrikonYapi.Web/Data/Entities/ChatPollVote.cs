using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Bir kat malikinin bir sohbet anketinde kullandığı oy.
    /// (ChatPollId, OwnerId) üzerinde benzersiz indeks vardır: malik oyunu sonradan değiştirebilir
    /// ama aynı ankette yalnızca bir satırı olur.</summary>
    public class ChatPollVote
    {
        public int Id { get; set; }

        [Required] public int ChatPollId { get; set; }
        public ChatPoll? ChatPoll { get; set; }

        [Required] public int ChatPollOptionId { get; set; }
        public ChatPollOption? ChatPollOption { get; set; }

        [Required] public int OwnerId { get; set; }
        public Owner? Owner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
