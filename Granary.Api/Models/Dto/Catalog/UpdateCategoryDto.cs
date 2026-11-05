namespace Granary.Api.Models.Dto.Catalog
{
    public record UpdateCategoryDto(string Name, int DisplayOrder, string? ImageUrl, IFormFile? ImageFile);
}
