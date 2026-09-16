using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Bir fon borçlusunun bağlı olduğu fon türü. Şu an sadece "Arsa Sahibi" destekleniyor,
    /// ileride Müteahhit/Yatırımcı-Ortak gibi türler eklenebilir.</summary>
    public enum PaymentFundType { ArsaSahibi = 0 }

    /// <summary>Bir bağımsız bölüme (Unit) bağlı olmayan, proje bazlı taksitli borç planı sahibi
    /// (ör. arsa sahibi). Kat Malikleri listesinde kayıtlı olmayabilir, bu yüzden ad serbest metin
    /// olarak tutulur.</summary>
    public class FundDebtor
    {
        public int Id { get; set; }

        [Required] public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required] public PaymentFundType FundType { get; set; } = PaymentFundType.ArsaSahibi;

        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<PaymentSchedule> PaymentSchedules { get; set; } = new List<PaymentSchedule>();
    }
}
