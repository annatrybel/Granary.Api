using Microsoft.AspNetCore.Identity;

namespace Granary.Api.Models.DatabaseModels
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? AvatarUrl { get; set; }
        public bool DarkMode { get; set; } = false;
        public string Language { get; set; } = "pl";
        public bool ExpiryNotifications { get; set; } = true;

        public ICollection<PantryItem> PantryItems { get; set; } = new List<PantryItem>();
        public ICollection<Recipe> CreatedRecipes { get; set; } = new List<Recipe>();
        public ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();
    }
}
