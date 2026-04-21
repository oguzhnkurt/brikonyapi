namespace BrikonYapi.Web.Data.Entities
{
    public enum CatalogType { Manual = 0, Auto = 1 }

    public class Catalog
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public CatalogType Type { get; set; } = CatalogType.Manual;
        public string? PdfPath { get; set; }
        public bool IsActive { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
