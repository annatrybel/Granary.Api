using Microsoft.AspNetCore.Identity;

namespace Granary.Api.Models.DatabaseModels
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? AvatarUrl { get; set; }
        public bool DarkMode { get; set; } = false;
        public string Language { get; set; } = "pl";
        public bool ExpiryNotifications { get; set; } = true;
        public Guid? DietId { get; set; }
        public Diet? Diet { get; set; }

        public ICollection<PantryItem> PantryItems { get; set; } = new List<PantryItem>();
        public ICollection<Recipe> CreatedRecipes { get; set; } = new List<Recipe>();
    }
}
