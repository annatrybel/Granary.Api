namespace Granary.Api.Models.Dto.Recipes
{
    public class RecipeFilterQueryDto
    {
        public string Scope { get; set; } = "all"; 
        public string? Query { get; set; }
        public string? Items { get; set; }
        public int? MaxTime { get; set; }
        public string? SortBy { get; set; } = "match"; 
        public string? MealType { get; set; }
        public string? Diet { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string Lang { get; set; } = "pl";
    }
    
}