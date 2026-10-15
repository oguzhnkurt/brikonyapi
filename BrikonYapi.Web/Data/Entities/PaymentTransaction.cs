using System.ComponentModel.DataAnnotations.Schema;

namespace BrikonYapi.Web.Data.Entities
{
    public enum PaymentMethod { BankTransfer = 0, CreditCard = 1 }
    /// <summary>Initiated: kredi kartı ödeme formu açıldı, sonuç henüz bilinmiyor (TKPAY sorgusu bekleniyor).</summary>
    public enum PaymentTransactionStatus { PendingApproval = 0, Approved = 1, Rejected = 2, Completed = 3, Failed = 4, Initiated = 5 }

    /// <summary>
    /// Bir ödeme kalemine (PaymentSchedule) karşılık yapılan tekil ödeme girişimi/işlemi.
    /// Havale/EFT: malik dekont yükler, admin onaylar (Approved/Rejected).
    /// Kredi kartı (TKPAY Ödeme Formu): Initiated → (TKPAY sorgusuyla doğrulanınca) Completed / Failed.
    /// Kart bilgisi bu sisteme hiç gelmez; yalnızca TKPAY'in döndüğü maskeli/özet bilgiler saklanır.
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

        // ── Kredi kartı (TKPAY) alanları ──────────────────────────────
        /// <summary>Bizim ürettiğimiz, TKPAY'e gönderilen tekil sipariş numarası (max 20 karakter).</summary>
        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public string? ExternalId { get; set; }

        /// <summary>Karttan çekilen toplam tutar (taksit vade farkı dahil). Amount = vade farksız taksit tutarı.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ChargedAmount { get; set; }

        /// <summary>Kat malikine yansıtılan taksit vade farkı (ChargedAmount - Amount).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? InstallmentInterestAmount { get; set; }

        public int? InstallmentCount { get; set; }

        /// <summary>Kartın ilk 6 hanesi (BIN) — kart numarası saklanmaz.</summary>
        [System.ComponentModel.DataAnnotations.MaxLength(8)]
        public string? CardBin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedByUserId { get; set; }
    }
}
