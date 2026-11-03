using Granary.Api.Models.Dto;
using Granary.Api.Models.Enums;
using Granary.Api.Services;
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
        public async Task<IActionResult> GetMyItems([FromQuery] StorageLocation? location, [FromQuery] string lang = "pl")
        {
            var result = await _pantryService.GetUserItemsAsync(lang, location);
            return HandleServiceResult(result);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var result = await _pantryService.GetZeroWasteStatsAsync();
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Dodaje produkt (z katalogu LUB własny customowy) do spiżarni użytkownika.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddToPantry([FromBody] CreateOrUpdatePantryItemDto dto)
        {
            var result = await _pantryService.AddItemAsync(dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Edytuje produkt w spiżarni (ilość, datę ważności, lokalizację).
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateItem(Guid id, [FromBody] CreateOrUpdatePantryItemDto dto)
        {
            var result = await _pantryService.UpdateItemAsync(id, dto);
            return HandleServiceResult(result);
        }

        [HttpPost("{id:guid}/consume")]
        public async Task<IActionResult> ConsumeItem(Guid id)
        {
            var result = await _pantryService.ResolveItemAsync(id, PantryItemStatus.Consumed);
            return result.IsSuccess ? Ok() : HandleServiceResult(result);
        }

        [HttpPost("{id:guid}/waste")]
        public async Task<IActionResult> WasteItem(Guid id)
        {
            var result = await _pantryService.ResolveItemAsync(id, PantryItemStatus.Wasted);
            return result.IsSuccess ? Ok() : HandleServiceResult(result);
        }
                
        /// <summary>
        /// Usuwa produkt ze spiżarni (i automatycznie sprząta nieużywane customowe produkty z bazy).
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(Guid id)
        {
            var result = await _pantryService.DeleteItemAsync(id);
            return HandleServiceResult(result);
        }

        
        public record GenerateProductImageRequest(string Name);


        [Authorize]
        [HttpPost("generate-product-image")]
        public async Task<IActionResult> GenerateProductImage(
            [FromBody] GenerateProductImageRequest request,
            [FromServices] IAiImageService imageService)
        {
            var result = await imageService.GenerateProductImageAsync(request.Name);

            if (!result.IsValid)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Ok(new { success = true, imageUrl = result.ImageUrl });
        }
    }
}