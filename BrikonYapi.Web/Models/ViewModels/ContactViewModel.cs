using System.ComponentModel.DataAnnotations;

namespace BrikonYapi.Web.Models.ViewModels
{
    public class ContactViewModel
    {
        [Required(ErrorMessage = "Ad Soyad gereklidir"), MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta gereklidir"), EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Subject { get; set; }

        [Required(ErrorMessage = "Mesaj gereklidir")]
        public string Message { get; set; } = string.Empty;
    }
}
