using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Konsept oylamasında bir konseptin malzeme kartındaki tek satır
    /// (ör. Parke — Meşe Naturel, Kod: AC4-123).</summary>
    public class PollOptionMaterial
    {
        public int Id { get; set; }

        [Required] public int PollOptionId { get; set; }
        public PollOption? PollOption { get; set; }

        /// <summary>Malzeme grubu: Parke, Fayans, Duvar Boyası, Dış Cephe...</summary>
        [Required, MaxLength(60)] public string Category { get; set; } = string.Empty;

        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;

        /// <summary>Marka / ürün kodu (opsiyonel).</summary>
        [MaxLength(150)] public string? Code { get; set; }

        /// <summary>Renk kodu (#RRGGBB) — boya gibi düz renkler için küçük renk kutusu (opsiyonel).</summary>
        [MaxLength(9)] public string? ColorHex { get; set; }

        /// <summary>Küçük doku görseli (opsiyonel).</summary>
        [MaxLength(500)] public string? SwatchPath { get; set; }

        public int OrderIndex { get; set; }
    }
}
