using Granary.Api.Models.Enums;

namespace Granary.Api.Models.Dto.Recipes
{
    public class RecipeMatchDto
    {
        public Guid RecipeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int PrepTimeMinutes { get; set; }
        public int Servings { get; set; }

        public int MatchPercentage { get; set; }
        public int OwnedIngredientsCount { get; set; }
        public int TotalIngredientsCount { get; set; }
        public int ExpiringIngredientsSaved { get; set; }
        public bool CanBeCookedNow { get; set; }

        public string UsedIngredientsSummary { get; set; } = string.Empty;

        public string DurationAndPortions => $"{PrepTimeMinutes} min • {Servings} {(Servings == 1 ? "porcja" : "porcje")}";

        public List<MissingIngredientDto> MissingIngredients { get; set; } = new();
    }

    public class MissingIngredientDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class RecipeDetailDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int PrepTimeMinutes { get; set; }
        public int Servings { get; set; }

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


