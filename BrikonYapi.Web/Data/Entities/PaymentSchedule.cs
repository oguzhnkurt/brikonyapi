using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrikonYapi.Web.Data.Entities
{
    public enum PaymentScheduleStatus { Pending = 0, Paid = 1, Overdue = 2, Cancelled = 3 }

    /// <summary>Hakedişe göre bir bağımsız bölüm için tanımlanan tek bir ödeme kalemi/taksiti.</summary>
    public class PaymentSchedule
    {
        public int Id { get; set; }

        [Required] public int UnitId { get; set; }
        public Unit? Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required] public DateTime DueDate { get; set; }

        [MaxLength(300)] public string? Description { get; set; }

        public PaymentScheduleStatus Status { get; set; } = PaymentScheduleStatus.Pending;

        /// <summary>Taksit sırası (Taksit 1, Taksit 2 ...). 0 ise vade tarihine göre sıralanır.</summary>
        public int InstallmentNo { get; set; } = 0;

        /// <summary>Bu taksidin karşılık geldiği hakediş yüzdesi (ör. %20). Taksit detayında gösterilir.
        /// Bir ProjectStage'e bağlıysa o aşamanın ThresholdPercentage'ından otomatik doldurulur.</summary>
        public int? HakedisPercentage { get; set; }

        /// <summary>Bu taksidin tetikleyicisi olan inşaat aşaması (opsiyonel). Bağlıysa, admin bu aşamayı
        /// "Tamamlandı" işaretlediğinde taksit malikin ekranında "İlerleme tamamlandı, ödeme bekleniyor"
        /// rozetiyle vurgulanır ve malike bildirim gönderilir.</summary>
        public int? ProjectStageId { get; set; }
        public ProjectStage? ProjectStage { get; set; }

        /// <summary>Ödemenin gerçekleştiği tarih (onaylanan işlem sonrası doldurulur).</summary>
        public DateTime? PaidAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
    }
}
