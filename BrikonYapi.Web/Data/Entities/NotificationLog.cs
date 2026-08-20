namespace BrikonYapi.Web.Data.Entities
{
    public enum NotificationChannel { Sms = 0, Email = 1 }
    public enum NotificationStatus { Sent = 0, Failed = 1, Pending = 2 }

    /// <summary>Bir malike gönderilen SMS/e-posta bildiriminin kaydı (Aşama 5 - SMS/e-posta entegrasyonu henüz aktif değil).</summary>
    public class NotificationLog
    {
        public int Id { get; set; }

        public int OwnerId { get; set; }
        public Owner? Owner { get; set; }

        public int? RelatedPaymentScheduleId { get; set; }
        public PaymentSchedule? RelatedPaymentSchedule { get; set; }

        public NotificationChannel Channel { get; set; }
        public string? Subject { get; set; }
        public string Message { get; set; } = string.Empty;

        public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? SentAt { get; set; }
    }
}
