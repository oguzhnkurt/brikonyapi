using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    public class Category
    {
        public int Id { get; set; }
        [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
        public int OrderIndex { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}
