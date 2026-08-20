using System.ComponentModel.DataAnnotations.Schema;

namespace BrikonYapi.Web.Data.Entities
{
    public enum PaymentMethod { BankTransfer = 0, CreditCard = 1 }
    public enum PaymentTransactionStatus { PendingApproval = 0, Approved = 1, Rejected = 2, Completed = 3, Failed = 4 }

    /// <summary>
    /// Bir ödeme kalemine (PaymentSchedule) karşılık yapılan tekil ödeme girişimi/işlemi.
    /// Havale/EFT: malik dekont yükler, admin onaylar (Approved/Rejected).
    /// Kredi kartı: sanal pos sağlayıcısı seçildiğinde ProviderTransactionId ile eşleştirilir (Aşama 4 - henüz aktif değil).
    /// </summary>
    public class PaymentTransaction
    {
        public int Id { get; set; }

        public int PaymentScheduleId { get; set; }
        public PaymentSchedule? PaymentSchedule { get; set; }

        public PaymentMethod Method { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>Havale/EFT dekont dosya yolu.</summary>
        public string? ReceiptFilePath { get; set; }

        /// <summary>Sanal pos sağlayıcısından dönen işlem/referans numarası (kredi kartı ödemeleri için).</summary>
        public string? ProviderTransactionId { get; set; }

        public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.PendingApproval;

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedByUserId { get; set; }
    }
}
