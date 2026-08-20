using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Şantiyeden çekilmiş tarihli ilerleme fotoğrafı (kat maliki İlerleme ekranında galeri olarak gösterilir).</summary>
    public class SitePhoto
    {
        public int Id { get; set; }

        [Required] public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(500)] public string ImagePath { get; set; } = string.Empty;

        [MaxLength(200)] public string? Caption { get; set; }

        /// <summary>Fotoğrafın çekildiği tarih (galeride kart üzerinde gösterilir).</summary>
        public DateTime TakenAt { get; set; } = DateTime.Now;

        /// <summary>360 derece panoramik kare ise rozet gösterilir.</summary>
        public bool Is360 { get; set; } = false;

        public int OrderIndex { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
