using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Contact
{
    public class ContactMessageDto
    {
        [Required(ErrorMessage = "Imię i nazwisko są wymagane.")]
        [StringLength(100, ErrorMessage = "Imię nie może przekraczać 100 znaków.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adres e-mail jest wymagany.")]
        [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Treść wiadomości jest wymagana.")]
        [StringLength(2000, ErrorMessage = "Wiadomość nie może przekraczać 2000 znaków.")]
        public string Message { get; set; } = string.Empty;
    }
}