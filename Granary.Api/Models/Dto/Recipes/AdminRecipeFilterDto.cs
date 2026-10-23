namespace Granary.Api.Models.Dto.Recipes
{
    public class AdminRecipeFilterDto
    {
        public string? Search { get; set; }
        public string? MealType { get; set; }
        public string? Diet { get; set; }
        public string? Status { get; set; } 
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
