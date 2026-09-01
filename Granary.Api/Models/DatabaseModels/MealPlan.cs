using Granary.Api.Models.Enums;

namespace Granary.Api.Models.DatabaseModels
{
    public class MealPlan
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public decimal? OptimizationScore { get; set; }
        public MealPlanStatus Status { get; set; } = MealPlanStatus.Planned;

        public ICollection<MealPlanRecipe> MealPlanRecipes { get; set; } = new List<MealPlanRecipe>();
    }
}
