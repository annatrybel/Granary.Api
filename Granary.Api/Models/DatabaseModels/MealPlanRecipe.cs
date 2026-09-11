using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Granary.Api.Models.Enums;

namespace Granary.Api.Models.DatabaseModels
{
    [Table("MealPlanRecipes")]
    public class MealPlanRecipe
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid MealPlanId { get; set; }

        [ForeignKey(nameof(MealPlanId))]
        public MealPlan MealPlan { get; set; } = null!;

        [Required]
        public Guid RecipeId { get; set; }

        [ForeignKey(nameof(RecipeId))]
        public Recipe Recipe { get; set; } = null!;

        public DateOnly PlannedDate { get; set; }
        public MealType MealType { get; set; } = MealType.Dinner;
    }
}
