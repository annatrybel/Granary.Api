using Microsoft.AspNetCore.Http;
using Granary.Api.Models.Dto.Recipes;

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

        /// <summary>Identyfikator GUID wybranej diety ze słownika Diets.</summary>
        public Guid? DietId { get; set; }

        /// <summary>Opcjonalny obiekt szczegółów diety (nazwa, kod).</summary>
        public DietDto? Diet { get; set; }
    }

    public record UpdateDarkModeDto(bool DarkMode);

    public record UpdateLanguageDto(string Language);

    /// <summary>DTO aktualizacji diety przyjmujące GUID wybranej diety (lub null dla braku diety).</summary>
    public record UpdateDietaryPreferenceDto(Guid? DietId);

    public record SendInvitationRequestDto(string RecipientEmail, string PantryName);
}