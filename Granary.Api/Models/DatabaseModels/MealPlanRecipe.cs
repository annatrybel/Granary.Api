using Granary.Api.Models.Enums;

namespace Granary.Api.Models.DatabaseModels
{
    public class MealPlanRecipe
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;

        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public DateOnly PlannedDate { get; set; }
        public MealType MealType { get; set; } = MealType.Dinner;
    }
}
