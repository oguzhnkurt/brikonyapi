using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Kat Maliki Portalı "Sıkça Sorulan Sorular" bölümü: kalın soru başlığı, tıklanınca açılan makale tarzı cevap.</summary>
    public class FaqItem
    {
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Question { get; set; } = string.Empty;

        [Required, MaxLength(4000)]
        public string Answer { get; set; } = string.Empty;

        // İngilizce çeviriler opsiyonel — boş bırakılırsa site EN görünümünde Türkçe metin gösterilir
        // (bkz. Project.DescriptionEn ile aynı desen).
        [MaxLength(300)]
        public string? QuestionEn { get; set; }

        [MaxLength(4000)]
        public string? AnswerEn { get; set; }

        public bool IsActive { get; set; } = true;
        public int OrderIndex { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
