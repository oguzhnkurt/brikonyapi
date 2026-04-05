using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Data.Entities
{
    public class HeroSlide
    {
        public int Id { get; set; }

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(300)]
        public string? Subtitle { get; set; }

        [MaxLength(500)]
        public string? VideoPath { get; set; }

        [MaxLength(500)]
        public string? BackgroundImagePath { get; set; }

        [MaxLength(100)]
        public string? ButtonText { get; set; }

        [MaxLength(500)]
        public string? ButtonUrl { get; set; }

        public int OrderIndex { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }
}
