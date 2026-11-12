namespace Granary.Api.Models.Dto
{
    namespace Granary.Api.Models.Dto.Recipes
    {
        public class DeductedIngredientDto
        {
            public Guid ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public string Unit { get; set; } = string.Empty;
        }
    }
}
