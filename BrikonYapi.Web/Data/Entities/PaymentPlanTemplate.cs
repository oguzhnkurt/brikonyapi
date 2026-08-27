using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Bir şablonun taksitlerinin nasıl tetikleneceğini belirler.</summary>
    public enum PaymentPlanType
    {
        /// <summary>Hakediş/Pursantaj bazlı: her kalem bir inşaat aşamasının tamamlanmasına bağlıdır
        /// (ör. "Yapı Ruhsatı %10", "2. Normal Kat Betonu %10"). Aşama "Tamamlandı" işaretlenince
        /// malike bildirim gider.</summary>
        StageBased = 0,

        /// <summary>Takvim/aylık bazlı: bir peşinat + belirli bir aydan başlayan aylık taksitler
        /// (ör. "10. aydan itibaren aylık %5").</summary>
        CalendarBased = 1
    }

    /// <summary>Bir projeye özel, tekrar kullanılabilir ödeme planı şablonu. Projedeki bir grup
    /// bağımsız bölüme toplu olarak uygulanır — her bölümün ContractAmount'ı × şablon kalemlerinin
    /// yüzdesi ile o bölüme özel PaymentSchedule satırları otomatik üretilir.</summary>
    public class PaymentPlanTemplate
    {
        public int Id { get; set; }

        [Required] public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;

        public PaymentPlanType PlanType { get; set; } = PaymentPlanType.StageBased;

        /// <summary>Şablondan üretilecek taksitlerin para birimi.</summary>
        public PaymentCurrency Currency { get; set; } = PaymentCurrency.TRY;

        [MaxLength(500)] public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PaymentPlanTemplateItem> Items { get; set; } = new List<PaymentPlanTemplateItem>();
    }
}
