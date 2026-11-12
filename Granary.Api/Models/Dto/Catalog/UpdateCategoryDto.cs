using Microsoft.AspNetCore.Http;

namespace Granary.Api.Models.Dto.Catalog
{
    public class UpdateCategoryDto
    {
        public string? Name { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsPantryStaple { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ImageUrl { get; set; }
    }
}
