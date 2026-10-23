namespace Granary.Api.Models.Dto.Recipes
{
    public class RecipeIngredientDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsOptional { get; set; }
        public bool IsOwnedInPantry { get; set; }
    }
}
