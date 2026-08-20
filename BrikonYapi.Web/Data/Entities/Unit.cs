using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Bir projeye bağlı bağımsız bölüm (daire/villa vb.).</summary>
    public class Unit
    {
        public int Id { get; set; }

        [Required] public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(50)] public string UnitNo { get; set; } = string.Empty;
        [MaxLength(50)] public string? BlockNo { get; set; }

        /// <summary>Kat numarası (ör. 3, -1 bodrum için).</summary>
        public int? FloorNo { get; set; }
        /// <summary>Oda düzeni (ör. "3+1").</summary>
        [MaxLength(20)] public string? RoomLayout { get; set; }
        /// <summary>Brüt metrekare.</summary>
        public int? AreaM2 { get; set; }

        /// <summary>0-100 arası inşaat ilerleme yüzdesi.</summary>
        public int ProgressPercentage { get; set; } = 0;
        public DateTime? ProgressUpdatedAt { get; set; }

        public int? OwnerId { get; set; }
        public Owner? Owner { get; set; }

        [MaxLength(1000)] public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PaymentSchedule> PaymentSchedules { get; set; } = new List<PaymentSchedule>();
    }
}
