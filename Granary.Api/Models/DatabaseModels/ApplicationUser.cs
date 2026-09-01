using Microsoft.AspNetCore.Identity;

namespace Granary.Api.Models.DatabaseModels
{
    public class ApplicationUser : IdentityUser
    {
        public string? AvatarUrl { get; set; }

        public ICollection<PantryItem> PantryItems { get; set; } = new List<PantryItem>();
        public ICollection<Recipe> CreatedRecipes { get; set; } = new List<Recipe>();
        public ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();
    }
}
