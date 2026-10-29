using Granary.Api.Models.Dto;
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
        /// Pobiera/Przeszukuje całą bazę produktów dla panelu administratora.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? query, [FromQuery] Guid? categoryId, [FromQuery] bool excludePantry = false) 
        {
            var result = await _catalogService.SearchAsync(query, categoryId, excludePantry);
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
        [HttpPut("{id}")]
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
        /// Usuwa dowolny produkt z katalogu systemowego.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
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