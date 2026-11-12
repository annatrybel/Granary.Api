namespace Granary.Api.Models.Dto.Catalog
{
    public class AdminCatalogProductsQueryDto : PagedQueryDto
    {
        public Guid? CategoryId { get; set; }
        public bool? IsCustom { get; set; }
    }

    public class AdminCatalogProductsPageDto : PagedResult<CatalogProductDto>
    {
        public int CustomProductsCount { get; set; }
    }
}
