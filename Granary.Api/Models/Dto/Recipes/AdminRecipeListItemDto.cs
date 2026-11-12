namespace Granary.Api.Models.Dto.Recipes
{
    public class AdminRecipeListItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int PrepTimeMinutes { get; set; }
        public int Servings { get; set; }
        public int? Calories { get; set; }
        public int IngredientsCount { get; set; }
        public List<DietDto> Diets { get; set; } = new();

        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public int FavoritesCount { get; set; }
        public double? ApprovalPercentage { get; set; }
        public bool NeedsAttention { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
