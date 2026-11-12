namespace Granary.Api.Models.Dto.Recipes
{
    public class RecipeFilterQueryDto
    {
        public string? Query { get; set; }
        public string? Items { get; set; }
        public string? Scope { get; set; }
        public Guid? DietId { get; set; }

        public string? Diet { get; set; }

        public int? MaxTime { get; set; }
        public string? SortBy { get; set; }
        public string? Lang { get; set; } = "pl";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}