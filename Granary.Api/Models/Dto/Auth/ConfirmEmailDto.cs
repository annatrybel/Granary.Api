using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Auth
{
    public class ConfirmEmailDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;
    }
}