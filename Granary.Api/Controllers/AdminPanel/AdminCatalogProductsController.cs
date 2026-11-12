using Granary.Api.Controllers;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.AdminPanel
{
    [ApiController]
    [Authorize(Roles = "Administrator")]
    [Route("api/admin/catalog-products")]
    [Produces("application/json")]
    public class AdminCatalogProductsController : GranaryBaseController
    {
        private readonly ICatalogProductService _catalogService;

        public AdminCatalogProductsController(ICatalogProductService catalogService)
        {
            _catalogService = catalogService;
        }

        /// <summary>
        /// Pobiera stronę produktów z całego katalogu (łącznie z produktami użytkowników) dla panelu administratora.
        /// Obsługuje filtrowanie po nazwie, kategorii oraz typie (system / custom).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(AdminCatalogProductsPageDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] AdminCatalogProductsQueryDto query, [FromQuery] string lang = "pl")
        {
            var result = await _catalogService.SearchForAdminAsync(query, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Dodaje oficjalny, globalny produkt systemowy do bazy.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(CatalogProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateGlobal([FromBody] CreateOrUpdateCatalogProductDto dto)
        {
            var result = await _catalogService.CreateAsync(dto, currentUserId: null);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Edytuje dowolny produkt w katalogu systemowym.
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(CatalogProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateGlobal(Guid id, [FromBody] CreateOrUpdateCatalogProductDto dto)
        {
            var result = await _catalogService.UpdateAsync(id, dto, currentUserId: null, isAdmin: true);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Usuwa dowolny produkt z katalogu systemowego (jeśli nie posiada aktywnych powiązań).
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteGlobal(Guid id)
        {
            var result = await _catalogService.DeleteAsync(id, currentUserId: null, isAdmin: true);
            return HandleServiceResult(result);
        }
    }
}