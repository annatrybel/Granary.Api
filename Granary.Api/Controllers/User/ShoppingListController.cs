using Granary.Api.Models.Dto.Shopping;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.Mobile
{
    /// <summary>
    /// Kontroler odpowiedzialny za zarządzanie listami zakupów użytkownika oraz ich pozycjami.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/shopping-lists")]
    [Produces("application/json")]
    public class ShoppingListController : GranaryBaseController
    {
        private readonly IShoppingListService _shoppingService;

        public ShoppingListController(IShoppingListService shoppingService)
        {
            _shoppingService = shoppingService;
        }

        #region Listy Zakupów

        /// <summary>
        /// Pobiera wszystkie listy zakupów należące do zalogowanego użytkownika.
        /// </summary>
        /// <remarks>
        /// Jeśli użytkownik nie posiada jeszcze żadnej listy, metoda automatycznie utworzy dla niego 'Główną listę' (IsMain = true).
        /// </remarks>
        /// <param name="lang">Kod języka (np. "pl", "en"), w którym ma zostać zwrócona przetłumaczona nazwa listy domyślnej.</param>
        /// <returns>Lista podsumowań list zakupów.</returns>
        /// <response code="200">Zwrócono listę podsumowań.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ShoppingListSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyLists([FromQuery] string lang = "pl")
        {
            var result = await _shoppingService.GetUserListsAsync(lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera szczegóły konkretnej listy zakupów wraz ze wszystkimi jej pozycjami.
        /// </summary>
        /// <param name="listId">Identyfikator GUID listy zakupów.</param>
        /// <param name="lang">Kod języka (np. "pl", "en") do przetłumaczenia nazw produktów i jednostek.</param>
        /// <returns>Szczegółowe dane listy z tablicą produktów.</returns>
        /// <response code="200">Pomyślnie pobrano szczegóły listy.</response>
        /// <response code="404">Wskazana lista nie istnieje lub nie należy do użytkownika.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpGet("{listId:guid}")]
        [ProducesResponseType(typeof(ShoppingListDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetListDetails(Guid listId, [FromQuery] string lang = "pl")
        {
            var result = await _shoppingService.GetListDetailsAsync(listId, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Tworzy nową, niestandardową listę zakupów.
        /// </summary>
        /// <param name="dto">Model zawierający nazwę nowej listy.</param>
        /// <returns>Nowo utworzona lista zakupów.</returns>
        /// <response code="201">Lista zakupów została pomyślnie utworzona.</response>
        /// <response code="400">Błędne dane wejściowe (np. pusta nazwa).</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ShoppingListSummaryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateList([FromBody] CreateShoppingListDto dto)
        {
            var result = await _shoppingService.CreateListAsync(dto);
            return HandleServiceResult(result, customStatusCode: StatusCodes.Status201Created);
        }

        /// <summary>
        /// Usuwa wybraną listę zakupów.
        /// </summary>
        /// <remarks>
        /// Usunięcie 'Głównej listy' (IsMain = true) jest zablokowane i zwróci błąd operacji.
        /// </remarks>
        /// <param name="listId">Identyfikator GUID usuwanej listy.</param>
        /// <returns>Status operacji usunięcia.</returns>
        /// <response code="200">Lista została pomyślnie usunięta.</response>
        /// <response code="400">Próba usunięcia listy głównej.</response>
        /// <response code="404">Lista nie istnieje.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpDelete("{listId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteList(Guid listId)
        {
            var result = await _shoppingService.DeleteListAsync(listId);
            return HandleServiceResult(result);
        }

        #endregion

        #region Pozycje na Liście

        /// <summary>
        /// Dodaje nową pozycję do wskazanej listy zakupów.
        /// </summary>
        /// <remarks>
        /// Należy podać 'ProductId' z bazy katalogowej LUB własną nazwę tekstową 'CustomName'.
        /// Jeśli podano 'ProductId', nazwa i jednostka zostaną automatycznie dopasowane do wybranego języka.
        /// </remarks>
        /// <param name="listId">Identyfikator listy zakupów.</param>
        /// <param name="dto">Dane pozycji (ilość, jednostka, ID produktu lub nazwa własna).</param>
        /// <param name="lang">Kod języka (np. "pl", "en").</param>
        /// <returns>Dodana pozycja na liście.</returns>
        /// <response code="201">Pozycja została pomyślnie dodana.</response>
        /// <response code="400">Brak nazwy i ID produktu lub produkt nie istnieje.</response>
        /// <response code="404">Lista zakupów nie istnieje.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpPost("{listId:guid}/items")]
        [ProducesResponseType(typeof(ShoppingItemDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddItem(Guid listId, [FromBody] AddShoppingItemDto dto, [FromQuery] string lang = "pl")
        {
            var result = await _shoppingService.AddItemAsync(listId, dto, lang);
            return HandleServiceResult(result, customStatusCode: StatusCodes.Status201Created);
        }

        /// <summary>
        /// Aktualizuje pozycję na liście zakupów (nazwa, ilość, jednostka, status "kupione").
        /// </summary>
        /// <remarks>
        /// Pola, których nie przesłano, pozostają bez zmian.
        /// </remarks>
        /// <param name="listId">Identyfikator listy zakupów.</param>
        /// <param name="itemId">Identyfikator pozycji.</param>
        /// <param name="dto">Zmieniane pola pozycji.</param>
        /// <returns>Zaktualizowana pozycja.</returns>
        /// <response code="200">Pozycja została zaktualizowana.</response>
        /// <response code="404">Nie odnaleziono pozycji na wskazanej liście.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpPut("{listId:guid}/items/{itemId:guid}")]
        [ProducesResponseType(typeof(ShoppingItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateItem(Guid listId, Guid itemId, [FromBody] UpdateShoppingItemDto dto)
        {
            var result = await _shoppingService.UpdateItemAsync(listId, itemId, dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Usuwa pojedynczą pozycję z danej listy zakupów.
        /// </summary>
        /// <param name="listId">Identyfikator listy zakupów.</param>
        /// <param name="itemId">Identyfikator usuwanej pozycji.</param>
        /// <returns>Status operacji usunięcia.</returns>
        /// <response code="200">Pozycja została pomyślnie usunięta.</response>
        /// <response code="404">Nie odnaleziono pozycji na wskazanej liście.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpDelete("{listId:guid}/items/{itemId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteItem(Guid listId, Guid itemId)
        {
            var result = await _shoppingService.DeleteItemAsync(listId, itemId);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Usuwa masowo wybrane pozycje z danej listy zakupów.
        /// </summary>
        /// <param name="listId">Identyfikator listy zakupów.</param>
        /// <param name="itemIds">Kolekcja identyfikatorów pozycji przeznaczonych do usunięcia.</param>
        /// <returns>Status operacji usunięcia.</returns>
        /// <response code="200">Wybrane pozycje zostały usunięte.</response>
        /// <response code="400">Przekazano pustą listę identyfikatorów.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpPost("{listId:guid}/items/bulk-delete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteSelectedItems(Guid listId, [FromBody] List<Guid> itemIds)
        {
            var result = await _shoppingService.DeleteItemsAsync(listId, itemIds);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Przenosi kupione produkty z listy zakupów bezpośrednio do magazynu (spiżarni / lodówki).
        /// </summary>
        /// <remarks>
        /// Produkty są usuwane z listy zakupów i tworzone jako nowe aktywne wpisy w magazynie domowym
        /// z domyślnym lub wskazanym terminem ważności oraz lokalizacją (np. Lodówka, Zamrażarka, Spiżarnia).
        /// </remarks>
        /// <param name="listId">Identyfikator listy zakupów.</param>
        /// <param name="dto">Dane transferu (identyfikatory kupionych pozycji, lokalizacja docelowa, domyślna data ważności).</param>
        /// <returns>Status powodzenia transferu.</returns>
        /// <response code="200">Produkty zostały pomyślnie przeniesione do magazynu.</response>
        /// <response code="400">Pusta lista pozycji lub brak wskazanych pozycji.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpPost("{listId:guid}/items/bulk-transfer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> BulkTransfer(Guid listId, [FromBody] BulkTransferToPantryDto dto)
        {
            var result = await _shoppingService.BulkTransferToPantryAsync(listId, dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Hurtowe dodawanie wielu produktów do wybranej listy zakupów (np. brakujące składniki z przepisu).
        /// </summary>
        /// <remarks>
        /// Przyjmuje listę obiektów. Jeśli produkt posiada 'ProductId', nazwa zostanie pobrana z bazy.
        /// W przeciwnym razie brana jest wartość z 'CustomName'.
        /// </remarks>
        /// <param name="listId">Identyfikator docelowej listy zakupów.</param>
        /// <param name="items">Lista produktów do dodania.</param>
        /// <param name="lang">Kod języka (np. "pl", "en").</param>
        /// <returns>Liczba pomyślnie dodanych pozycji.</returns>
        /// <response code="201">Pozycje zostały pomyślnie dodane.</response>
        /// <response code="400">Pusta lista pozycji lub brak uprawnień.</response>
        /// <response code="404">Wskazana lista zakupów nie istnieje.</response>
        /// <response code="401">Użytkownik nie jest zalogowany.</response>
        [HttpPost("{listId:guid}/items/bulk")]
        [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddItemsBulk(
            Guid listId,
            [FromBody] List<AddShoppingItemDto> items,
            [FromQuery] string lang = "pl")
        {
            var result = await _shoppingService.AddItemsBulkAsync(listId, items, lang);
            return HandleServiceResult(result, customStatusCode: StatusCodes.Status201Created);
        }
        #endregion
    }
}