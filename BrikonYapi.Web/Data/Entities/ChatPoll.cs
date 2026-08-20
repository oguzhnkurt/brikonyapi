using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>
    /// Sohbet içinde yönetim tarafından oluşturulan WhatsApp tarzı hızlı anket.
    /// Ayrı "Oylama" (Poll) ekranından farklıdır: doğrudan proje sohbet akışının içinde,
    /// bir ChatMessage'a bağlı balon olarak görünür.
    /// </summary>
    public class ChatPoll
    {
        public int Id { get; set; }

        [Required] public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(300)] public string Question { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<ChatPollOption> Options { get; set; } = new List<ChatPollOption>();
        public ICollection<ChatPollVote> Votes { get; set; } = new List<ChatPollVote>();
    }
}
