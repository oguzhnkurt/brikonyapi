using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    public class SiteSetting
    {
        public int Id { get; set; }
        [Required, MaxLength(100)] public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        [MaxLength(200)] public string? Description { get; set; }
    }
}
