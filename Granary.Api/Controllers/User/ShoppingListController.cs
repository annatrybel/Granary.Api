using Granary.Api.Models.Dto.Shopping;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.Mobile
{
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

        [HttpGet]
        public async Task<IActionResult> GetMyLists([FromQuery] string lang = "pl")
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.GetUserListsAsync(userId, lang);
            return HandleServiceResult(result);
        }

        [HttpGet("{listId}")]
        public async Task<IActionResult> GetListDetails(Guid listId, [FromQuery] string lang = "pl")
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.GetListDetailsAsync(userId, listId, lang);
            return HandleServiceResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateList([FromBody] CreateShoppingListDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.CreateListAsync(userId, dto);
            return HandleServiceResult(result, customStatusCode: StatusCodes.Status201Created);
        }

        [HttpDelete("{listId}")]
        public async Task<IActionResult> DeleteList(Guid listId)
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.DeleteListAsync(userId, listId);
            return HandleServiceResult(result);
        }

        #endregion

        #region Pozycje na Liście

        [HttpPost("{listId}/items")]
        public async Task<IActionResult> AddItem(Guid listId, [FromBody] AddShoppingItemDto dto, [FromQuery] string lang = "pl")
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.AddItemAsync(userId, listId, dto, lang);
            return HandleServiceResult(result, customStatusCode: StatusCodes.Status201Created);
        }

        [HttpPost("{listId}/items/from-pantry/{pantryItemId}")]
        public async Task<IActionResult> AddFromPantry(Guid listId, Guid pantryItemId, [FromQuery] string lang = "pl")
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.AddFromPantryItemAsync(userId, listId, pantryItemId, lang);
            return HandleServiceResult(result);
        }

        [HttpDelete("{listId}/items/{itemId}")]
        public async Task<IActionResult> DeleteItem(Guid listId, Guid itemId)
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.DeleteItemAsync(userId, listId, itemId);
            return HandleServiceResult(result);
        }

        [HttpPost("{listId}/items/bulk-delete")]
        public async Task<IActionResult> DeleteSelectedItems(Guid listId, [FromBody] List<Guid> itemIds)
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.DeleteItemsAsync(userId, listId, itemIds);
            return HandleServiceResult(result);
        }

        [HttpPost("{listId}/items/bulk-transfer")]
        public async Task<IActionResult> BulkTransfer(Guid listId, [FromBody] BulkTransferToPantryDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _shoppingService.BulkTransferToPantryAsync(userId, listId, dto);
            return HandleServiceResult(result);
        }

        #endregion
    }
}