using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    public enum ProjectStageStatus { Pending = 0, InProgress = 1, Completed = 2 }

    /// <summary>Bir projenin inşaat aşaması (Planlama, Kazı, Temel, Karkas, Dış Cephe, İç Mekan, Bitirme, Teslim vb.).</summary>
    public class ProjectStage
    {
        public int Id { get; set; }

        [Required] public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;

        /// <summary>Listede gösterim sırası (1'den başlar).</summary>
        public int OrderIndex { get; set; } = 0;

        /// <summary>Bu aşamanın başladığı genel ilerleme eşiği (%). Kat maliki ekranında "%25+" olarak gösterilir.</summary>
        [Range(0, 100)] public int ThresholdPercentage { get; set; } = 0;

        public ProjectStageStatus Status { get; set; } = ProjectStageStatus.Pending;

        public DateTime? CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
