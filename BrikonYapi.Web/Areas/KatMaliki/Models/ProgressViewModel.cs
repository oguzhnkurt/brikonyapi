using BrikonYapi.Web.Data.Entities;

namespace BrikonYapi.Web.Areas.KatMaliki.Models
{
    /// <summary>Kat Maliki "İlerleme" ekranında tek bir projenin inşaat durumu.</summary>
    public class ProjectProgressViewModel
    {
        public Project Project { get; set; } = null!;

        /// <summary>Malikin bu projedeki bağımsız bölümleri (birden fazla olabilir).</summary>
        public List<Unit> Units { get; set; } = new();

        public List<ProjectStage> Stages { get; set; } = new();
        public List<SitePhoto> Photos { get; set; } = new();

        /// <summary>Genel ilerleme yüzdesi. Proje üzerinde tanımlı değilse maliklerin bölüm ortalaması kullanılır.</summary>
        public int OverallProgress { get; set; }

        public DateTime? EstimatedDelivery => Project.EstimatedDeliveryDate;

        /// <summary>GÜVENLİK: iframe'e yalnızca mutlak HTTPS adresi gömülür.
        /// javascript:, data: gibi şemalar ve göreli adresler reddedilir.</summary>
        public string? SafeVirtualTourUrl
        {
            get
            {
                var raw = Project.VirtualTourUrl;
                if (string.IsNullOrWhiteSpace(raw)) return null;
                if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)) return null;
                return uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : null;
            }
        }

        /// <summary>Şu an devam eden aşama (kart üstünde "Mevcut aşama" olarak gösterilir).</summary>
        public ProjectStage? CurrentStage =>
            Stages.FirstOrDefault(s => s.Status == ProjectStageStatus.InProgress)
            ?? Stages.LastOrDefault(s => s.Status == ProjectStageStatus.Completed);
    }

    /// <summary>Bir para birimi için ödenen/kalan toplamı. Taksitler TL/USD/EUR karışık olabildiğinden
    /// (bkz. Admin taksit sihirbazı) toplamlar para birimine göre ayrı tutulur, birbirine katılmaz.</summary>
    public class CurrencyPaymentTotal
    {
        public PaymentCurrency Currency { get; set; }
        public decimal Paid { get; set; }
        public decimal Remaining { get; set; }
        public string Symbol => Currency switch { PaymentCurrency.USD => "$", PaymentCurrency.EUR => "€", _ => "₺" };
    }

    public class ProgressPageViewModel
    {
        public Owner Owner { get; set; } = null!;
        public List<ProjectProgressViewModel> Projects { get; set; } = new();

        // ── Ana sayfa alt bölümü: ödeme özeti + haberler ─────────
        public List<CurrencyPaymentTotal> PaymentTotals { get; set; } = new();
        public bool HasAnySchedule { get; set; }
        public List<Announcement> RecentNews { get; set; } = new();
    }
}
