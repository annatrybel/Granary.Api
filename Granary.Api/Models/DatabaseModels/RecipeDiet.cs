namespace Granary.Api.Models.DatabaseModels
{
    public class RecipeDiet
    {
        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public Guid DietId { get; set; }
        public Diet Diet { get; set; } = null!;
    }
}