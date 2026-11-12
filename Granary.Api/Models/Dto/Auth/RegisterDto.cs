using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Auth
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
        [EmailAddress(ErrorMessage = "Niepoprawny format adresu e-mail.")]
        public string Email { get; set; } = string.Empty;

        public string? Username { get; set; }

        public string? Password { get; set; }

        public string? FrontendOrigin { get; set; }
    }
}
