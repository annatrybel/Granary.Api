using Microsoft.AspNetCore.Http;
using Granary.Api.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Profile;

public class UpdateProfileDto
{
    public string? Username { get; set; }
    public IFormFile? Avatar { get; set; }
    [EnumDataType(typeof(DietaryPreference))]
    public DietaryPreference DietaryPreference { get; set; } = DietaryPreference.Standard;
}

public record UpdateProfileResponseDto(string? AvatarUrl, string? Username = null);