using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Data.Entities
{
    /// <summary>Kat maliki profili. AspNetUsers tablosundaki IdentityUser ile UserId üzerinden eşleşir.</summary>
    public class Owner
    {
        public int Id { get; set; }

        [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
        [MaxLength(30)]  public string? Phone { get; set; }
        [MaxLength(150)] public string? Email { get; set; }

        /// <summary>Malikin T.C. Kimlik Numarası — Kat Maliki Portalı'na giriş şifresi olarak kullanılır
        /// (bkz. PhoneNormalizer, OwnersController). 11 haneli, sadece rakam.</summary>
        [MaxLength(11)] public string? TcKimlikNo { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Unit> Units { get; set; } = new List<Unit>();
    }
}
