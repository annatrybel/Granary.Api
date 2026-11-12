namespace Granary.Api.Models.Dto.Recipes
{
    public class DietDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}