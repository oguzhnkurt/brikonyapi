using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    public enum ProjectStatus { Ongoing = 0, Completed = 1 }


    public class Project
    {
        public int Id { get; set; }
        [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Slug { get; set; } = string.Empty;
        [MaxLength(500)] public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        [MaxLength(200)] public string? Location { get; set; }
        [MaxLength(100)] public string? District { get; set; }
        [MaxLength(100)] public string? City { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Ongoing;
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        public int? TotalArea { get; set; }
        public int? UnitCount { get; set; }
        public int? FloorCount { get; set; }
        public int? BlockCount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        [MaxLength(80)]  public string? CardTag { get; set; }
        [MaxLength(500)] public string? MainImagePath { get; set; }
        [MaxLength(500)] public string? VideoPath { get; set; }
        public bool CardVideoAutoplay { get; set; } = false; // Ana sayfada kart: video mu fotoğraf mı
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
        public bool IsMarquee { get; set; } = true; // Kayan bant (marquee) bölümünde göster
        public int OrderIndex { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public ICollection<ProjectImage> Images { get; set; } = new List<ProjectImage>();
        public ICollection<HeroSlide> HeroSlides { get; set; } = new List<HeroSlide>();
    }
}
