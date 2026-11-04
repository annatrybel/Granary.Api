using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Pantry;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Ai;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.Mobile
{
    /// <summary>
    /// Zarządzanie produktami w spiżarni, zamrażarce i lodówce użytkownika.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/pantry/items")]
    [Produces("application/json")]
    public class PantryItemsController : GranaryBaseController
    {
        private readonly IPantryItemService _pantryService;
        private readonly IAiImageService _imageService;

        public PantryItemsController(
            IPantryItemService pantryService,
            IAiImageService imageService)
        {
            _pantryService = pantryService;
            _imageService = imageService;
        }

        /// <summary>
        /// Pobiera listę aktywnych produktów w spiżarni zalogowanego użytkownika.
        /// </summary>
        /// <remarks>
        /// Zwraca tylko aktywne produkty (nieprzeterminowane lub niewyrzucone/niezużyte).
        /// Umożliwia filtrowanie według miejsca przechowywania (Lodówka, Zamrażarka, Spiżarnia).
        /// </remarks>
        /// <param name="location">Opcjonalny filtr lokalizacji (np. Fridge, Freezer, Pantry).</param>
        /// <param name="lang">Język tłumaczenia nazw produktów (domyślnie "pl").</param>
        /// <returns>Lista pozycji w magazynie użytkownika.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<PantryItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyItems(
            [FromQuery] StorageLocation? location,
            [FromQuery] string lang = "pl")
        {
            var result = await _pantryService.GetUserItemsAsync(lang, location);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera statystyki Zero-Waste dla bieżącego miesiąca.
        /// </summary>
        /// <remarks>
        /// Zwraca liczbę produktów oznaczonych w tym miesiącu jako zużyte (Consumed) 
        /// oraz zmarnowane (Wasted), wraz z procentowym wskaźnikiem wykorzystania żywności.
        /// </remarks>
        /// <returns>Statystyki Zero-Waste użytkownika.</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(PantryZeroWasteStatsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetStats()
        {
            var result = await _pantryService.GetZeroWasteStatsAsync();
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Dodaje produkt do spiżarni (z katalogu ogólnego lub jako produkt własny).
        /// </summary>
        /// <remarks>
        /// Jeśli ten sam produkt znajduje się już w tej samej lokalizacji z aktywnym statusem, 
        /// jego ilość zostanie zsumowana zamiast tworzenia nowej pozycji.
        /// </remarks>
        /// <param name="dto">Dane dodawanego produktu (ilość, data ważności, lokalizacja, ew. zdjęcie).</param>
        /// <param name="lang">Język odpowiedzi (domyślnie "pl").</param>
        /// <returns>Utworzony lub zaktualizowany element spiżarni.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(PantryItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddToPantry(
            [FromBody] CreateOrUpdatePantryItemDto dto,
            [FromQuery] string lang = "pl")
        {
            var result = await _pantryService.AddItemAsync(dto, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Aktualizuje właściwości produktu w spiżarni.
        /// </summary>
        /// <remarks>
        /// Pozwala zmienić ilość, datę zakupu, datę ważności oraz lokalizację.
        /// W przypadku produktów własnych (customowych) aktualizuje również dane w katalogu (np. własne zdjęcie).
        /// </remarks>
        /// <param name="id">Identyfikator pozycji w spiżarni (PantryItemId).</param>
        /// <param name="dto">Zaktualizowane dane produktu.</param>
        /// <param name="lang">Język odpowiedzi (domyślnie "pl").</param>
        /// <returns>Zaktualizowany element spiżarni.</returns>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(PantryItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateItem(
            Guid id,
            [FromBody] CreateOrUpdatePantryItemDto dto,
            [FromQuery] string lang = "pl")
        {
            var result = await _pantryService.UpdateItemAsync(id, dto, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Oznacza produkt jako zużyty / zjedzony (Consumed).
        /// </summary>
        /// <param name="id">Identyfikator pozycji w spiżarni.</param>
        [HttpPost("{id:guid}/consume")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsumeItem(Guid id)
        {
            var result = await _pantryService.ResolveItemAsync(id, PantryItemStatus.Consumed);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Oznacza produkt jako zmarnowany / wyrzucony (Wasted).
        /// </summary>
        /// <param name="id">Identyfikator pozycji w spiżarni.</param>
        [HttpPost("{id:guid}/waste")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> WasteItem(Guid id)
        {
            var result = await _pantryService.ResolveItemAsync(id, PantryItemStatus.Wasted);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Trwale usuwa produkt ze spiżarni użytkownika.
        /// </summary>
        /// <remarks>
        /// Jeśli usuwany produkt był produktem własnym użytkownika i nie jest już używany 
        /// w żadnym przepisie ani liście zakupów, zostanie on automatycznie wyczyszczony z bazy 
        /// wraz z powiązanym plikiem graficznym.
        /// </remarks>
        /// <param name="id">Identyfikator pozycji w spiżarni.</param>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteItem(Guid id)
        {
            var result = await _pantryService.DeleteItemAsync(id);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Generuje realistyczne zdjęcie produktu spożywczego przy użyciu sztucznej inteligencji (AI Image Generator).
        /// </summary>
        /// <param name="request">Nazwa produktu, dla którego generowane jest zdjęcie.</param>
        /// <returns>Wygenerowany adres URL do pliku graficznego.</returns>
        [HttpPost("generate-product-image")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GenerateProductImage([FromBody] GenerateProductImageRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return HandleServiceResult(ServiceResult<string>.Failure("Nazwa produktu jest wymagana."));

            var result = await _imageService.GenerateProductImageAsync(request.Name);

            if (!result.IsValid)
                return HandleServiceResult(ServiceResult<string>.Failure(result.ErrorMessage ?? "Błąd generowania obrazu."));

            return HandleServiceResult(ServiceResult<string>.Success(result.ImageUrl!));
        }
    }
}