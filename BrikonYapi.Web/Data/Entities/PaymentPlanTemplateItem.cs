using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Bir ödeme planı şablonunun tek bir kalemi (ör. "Yapı Ruhsatı %10" ya da "Peşinat %20").
    /// StageBased şablonlarda ProjectStageId dolu olur; CalendarBased şablonlarda MonthOffset dolu olur.</summary>
    public class PaymentPlanTemplateItem
    {
        public int Id { get; set; }

        [Required] public int PaymentPlanTemplateId { get; set; }
        public PaymentPlanTemplate? PaymentPlanTemplate { get; set; }

        /// <summary>Listede gösterim/uygulama sırası (1'den başlar).</summary>
        public int OrderIndex { get; set; } = 0;

        [Required, MaxLength(200)] public string Label { get; set; } = string.Empty;

        /// <summary>Bölümün ContractAmount tutarının kaçta kaçının bu kalemde tahsil edileceği (ör. 10 = %10).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal Percentage { get; set; }

        /// <summary>StageBased şablonlarda: bu kalemin tetikleyicisi olan proje inşaat aşaması.
        /// Aşama tamamlandığında ilgili PaymentSchedule satırı malike bildirimle vurgulanır.</summary>
        public int? ProjectStageId { get; set; }
        public ProjectStage? ProjectStage { get; set; }

        /// <summary>CalendarBased şablonlarda: atama tarihinden itibaren kaç ay sonra vadesi gelir
        /// (0 = peşinat, atama anında). Üretilen PaymentSchedule.DueDate bu değerden hesaplanır,
        /// ardından elle değiştirilebilir.</summary>
        public int? MonthOffset { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
