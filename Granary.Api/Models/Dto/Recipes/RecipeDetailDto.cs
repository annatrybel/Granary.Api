namespace Granary.Api.Models.Dto.Recipes
{
    public class RecipeDetailDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int PrepTimeMinutes { get; set; }
        public int Servings { get; set; }

        public bool IsFavorite { get; set; }
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public bool? UserVote { get; set; }
        public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    }

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
