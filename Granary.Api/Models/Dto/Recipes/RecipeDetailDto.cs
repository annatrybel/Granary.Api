namespace Granary.Api.Models.Dto.Recipes
{
    public class RecipeDetailDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int PrepTimeMinutes { get; set; }
        public int Servings { get; set; }
        public int? Calories { get; set; }

        public bool IsFavorite { get; set; }
        public bool IsCustom { get; set; }
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public bool? UserVote { get; set; }
        public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    }
}
