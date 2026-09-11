using Granary.Api.Models.Dto;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.Mobile
{
    [ApiController]
    [Authorize]
    [Route("api/pantry/items")]
    [Produces("application/json")]
    public class PantryItemsController : GranaryBaseController
    {
        private readonly IPantryItemService _pantryService;

        public PantryItemsController(IPantryItemService pantryService)
        {
            _pantryService = pantryService;
        }

        /// <summary>
        /// Pobiera zapasy zalogowanego użytkownika.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyItems([FromQuery] StorageLocation? location)
        {
            var userId = GetCurrentUserId();
            var result = await _pantryService.GetUserItemsAsync(userId, location);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Dodaje produkt (z katalogu LUB własny customowy) do spiżarni użytkownika.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddToPantry([FromBody] CreateOrUpdatePantryItemDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _pantryService.AddItemAsync(userId, dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Edytuje produkt w spiżarni (ilość, datę ważności, lokalizację).
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateItem(Guid id, [FromBody] CreateOrUpdatePantryItemDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _pantryService.UpdateItemAsync(userId, id, dto);
            return HandleServiceResult(result);
        }
                
        /// <summary>
        /// Usuwa produkt ze spiżarni (i automatycznie sprząta nieużywane customowe produkty z bazy).
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _pantryService.DeleteItemAsync(userId, id);
            return HandleServiceResult(result);
        }
    }
}