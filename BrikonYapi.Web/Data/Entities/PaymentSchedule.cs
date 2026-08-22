using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrikonYapi.Web.Data.Entities
{
    public enum PaymentScheduleStatus { Pending = 0, Paid = 1, Overdue = 2, Cancelled = 3 }

    /// <summary>Bir taksidin/planın tahsil edildiği para birimi. Bazı projelerde ödeme dolar veya euro
    /// kuru üzerinden alınır — bu durumda Amount doğrudan o para biriminde saklanır, TL karşılığı
    /// hesaplanmaz (malik doğrudan dolar/euro öder).</summary>
    public enum PaymentCurrency { TRY = 0, USD = 1, EUR = 2 }

    /// <summary>Hakedişe göre bir bağımsız bölüm için tanımlanan tek bir ödeme kalemi/taksiti.</summary>
    public class PaymentSchedule
    {
        public int Id { get; set; }

        [Required] public int UnitId { get; set; }
        public Unit? Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>Bu taksidin para birimi. Plan bazında tek bir para birimi seçilir (bkz. Admin
        /// taksit sihirbazı) — aynı plandaki tüm taksitler aynı para biriminde olur.</summary>
        public PaymentCurrency Currency { get; set; } = PaymentCurrency.TRY;

        /// <summary>Görüntüleme için para birimi sembolü (₺/$/€). Veritabanına yazılmaz.</summary>
        [NotMapped]
        public string CurrencySymbol => Currency switch
        {
            PaymentCurrency.USD => "$",
            PaymentCurrency.EUR => "€",
            _ => "₺"
        };

        [Required] public DateTime DueDate { get; set; }

        [MaxLength(300)] public string? Description { get; set; }

        public PaymentScheduleStatus Status { get; set; } = PaymentScheduleStatus.Pending;

        /// <summary>Taksit sırası (Taksit 1, Taksit 2 ...). 0 ise vade tarihine göre sıralanır.</summary>
        public int InstallmentNo { get; set; } = 0;

        /// <summary>Bu taksidin karşılık geldiği hakediş yüzdesi (ör. %20). Elle girilir, taksit detayında gösterilir.</summary>
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
