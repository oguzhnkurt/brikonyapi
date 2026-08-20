using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Bir kat malikinin bir oylamada kullandığı oy.
    /// (PollId, OwnerId) üzerinde benzersiz indeks vardır: bir malik bir oylamada yalnızca bir kez oy kullanabilir.</summary>
    public class PollVote
    {
        public int Id { get; set; }

        [Required] public int PollId { get; set; }
        public Poll? Poll { get; set; }

        [Required] public int PollOptionId { get; set; }
        public PollOption? PollOption { get; set; }

        [Required] public int OwnerId { get; set; }
        public Owner? Owner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
