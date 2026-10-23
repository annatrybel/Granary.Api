using Microsoft.AspNetCore.Http;

namespace Granary.Api.Models.Dto.Profile
{
    public class UserProfileDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool DarkMode { get; set; }
        public string? Language { get; set; }
    }

    public record UpdateDarkModeDto(bool DarkMode);

    public record UpdateLanguageDto(string Language);
   
    public record SendInvitationRequestDto(string RecipientEmail, string PantryName);
}