using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Data.Entities
{
    public class ProjectImage
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(500)]
        public string ImagePath { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Caption { get; set; }

        public int OrderIndex { get; set; } = 0;
        public bool IsMain { get; set; } = false;
    }
}
