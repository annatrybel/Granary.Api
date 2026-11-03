using Granary.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace Granary.Api.Models.DatabaseModels
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? AvatarUrl { get; set; }
        public bool DarkMode { get; set; } = false;
        public string Language { get; set; } = "pl";
        public bool ExpiryNotifications { get; set; } = true;
        public DietaryPreference DietaryPreference { get; set; } = DietaryPreference.Standard;

        public ICollection<PantryItem> PantryItems { get; set; } = new List<PantryItem>();
        public ICollection<Recipe> CreatedRecipes { get; set; } = new List<Recipe>();
    }
}
