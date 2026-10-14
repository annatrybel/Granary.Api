using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.Mobile
{
    [ApiController]
    [Authorize]
    [Route("api/catalog-products")]
    [Produces("application/json")]
    public class CatalogProductsController : GranaryBaseController
    {
        private readonly ICatalogProductService _catalogService;

        public CatalogProductsController(ICatalogProductService catalogService)
        {
            _catalogService = catalogService;
        }

        /// <summary>
        /// Wyszukuje produkty w katalogu w zadanym języku (systemowe + własne customowe).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CatalogProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Search(
            [FromQuery] string? query,
            [FromQuery] Guid? categoryId,
            [FromQuery] bool excludePantry = false,
            [FromQuery] string lang = "pl") 
        {
            var userId = GetCurrentUserId();

            var result = await _catalogService.SearchAsync(query, categoryId, userId, excludePantry, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera szczegóły produktu z katalogu.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CatalogProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid id, [FromQuery] string lang = "pl")
        {
            var result = await _catalogService.GetByIdAsync(id, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Wyszukuje produkt po kodzie kreskowym.
        /// </summary>
        [HttpGet("barcode/{barcode}")]
        [ProducesResponseType(typeof(CatalogProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetByBarcode(string barcode, [FromQuery] string lang = "pl")
        {
            var result = await _catalogService.GetByBarcodeAsync(barcode, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera listę kategorii produktów posortowanych wg DisplayOrder.
        /// </summary>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategories([FromQuery] string lang = "pl")
        {
            var result = await _catalogService.GetCategoriesAsync(lang);
            return HandleServiceResult(result);
        }

        [HttpPost("custom")]
        [ProducesResponseType(typeof(CatalogProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCustom([FromBody] CatalogProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Nazwa produktu jest wymagana.");
            }

            var userId = GetCurrentUserId();

            var productId = await _catalogService.GetOrCreateCustomProductAsync(
                userId: userId,
                name: dto.Name,
                categoryId: dto.CategoryId != Guid.Empty ? dto.CategoryId : null,
                unit: dto.DefaultUnit,
                imageUrl: dto.ImageUrl
            );

            var result = await _catalogService.GetByIdAsync(productId);
            return HandleServiceResult(result);
        }
    }
}