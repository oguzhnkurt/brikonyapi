using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Yönetimin Kat Maliki Portalı ana sayfasında gösterdiği kısa duyuru/haber.</summary>
    public class Announcement
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string Body { get; set; } = string.Empty;

        /// <summary>Kategori rozeti (Haber, Bilgi, Uyarı, Kentsel Dönüşüm, Sektör vb.).</summary>
        [MaxLength(40)]
        public string? Tag { get; set; }

        // ── Haberler ekranı alanları ─────────────────────────────
        /// <summary>Haber kartının kapak görseli. Boşsa kart yalnızca metin olarak gösterilir.</summary>
        [MaxLength(500)]
        public string? CoverImagePath { get; set; }

        /// <summary>Kart üzerinde gösterilen kısa özet. Boşsa metnin ilk satırı kullanılır.</summary>
        [MaxLength(300)]
        public string? Summary { get; set; }

        /// <summary>Kaynak / yayınlayan (varsayılan: Brikon Yapı).</summary>
        [MaxLength(100)]
        public string? Source { get; set; }

        /// <summary>Kat maliki ana sayfasındaki kısa "Duyurular" bölümünde de gösterilsin mi?</summary>
        public bool ShowOnHome { get; set; } = true;

        public bool IsActive { get; set; } = true;
        public int OrderIndex { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
