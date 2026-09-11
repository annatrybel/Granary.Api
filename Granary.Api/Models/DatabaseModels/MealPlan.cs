using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Granary.Api.Models.Enums;

namespace Granary.Api.Models.DatabaseModels
{
    [Table("MealPlans")]
    public class MealPlan
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(5,2)")]
        public decimal? OptimizationScore { get; set; } 
        public MealPlanStatus Status { get; set; } = MealPlanStatus.Planned;

        public ICollection<MealPlanRecipe> MealPlanRecipes { get; set; } = new List<MealPlanRecipe>();
    }
}
