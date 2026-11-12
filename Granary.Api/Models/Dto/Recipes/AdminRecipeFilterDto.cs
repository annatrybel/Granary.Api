namespace Granary.Api.Models.Dto.Recipes
{
    public class AdminRecipeFilterDto
    {
        public string? Search { get; set; }
        public Guid? DietId { get; set; }
        public bool? NeedsAttention { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
