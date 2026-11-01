namespace Granary.Api.Models.Dto.Catalog
{
    public class AdminCatalogProductsQueryDto : PagedQueryDto
    {
        public Guid? CategoryId { get; set; }
    }

    public class AdminCatalogProductsPageDto : PagedResult<CatalogProductDto>
    {
        /// <summary>Liczba produktów dodanych przez użytkowników w całym katalogu (niezależnie od filtrów).</summary>
        public int CustomProductsCount { get; set; }
    }
}
