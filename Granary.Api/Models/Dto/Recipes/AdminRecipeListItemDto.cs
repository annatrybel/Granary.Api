namespace Granary.Api.Models.Dto.Recipes
{
    public class AdminRecipeListItemDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty; 
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; } 
        public string? ImageUrl { get; set; }
        public string Category { get; set; } = "Obiad"; 
        public List<string> MainIngredients { get; set; } = new();
        public int ExtraIngredientsCount { get; set; }
        public int PrepTimeMinutes { get; set; }
        public int Servings { get; set; }
        public string Difficulty { get; set; } = "Średni"; 
        public double? Rating { get; set; } 
        public int ReviewsCount { get; set; } 
        public int CookedCount { get; set; } 
        public bool IsOfficial { get; set; }
        public bool IsNew { get; set; } 
    }
}
