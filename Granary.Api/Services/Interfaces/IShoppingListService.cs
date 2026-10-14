using Granary.Api.Models.Dto.Shopping;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IShoppingListService
    {
        Task<ServiceResult<IEnumerable<ShoppingListSummaryDto>>> GetUserListsAsync(Guid userId, string lang = "pl");
        Task<ServiceResult<ShoppingListDetailDto>> GetListDetailsAsync(Guid userId, Guid listId, string lang = "pl");
        Task<ServiceResult<ShoppingListSummaryDto>> CreateListAsync(Guid userId, CreateShoppingListDto dto);
        Task<ServiceResult<bool>> DeleteListAsync(Guid userId, Guid listId);

        Task<ServiceResult<ShoppingItemDto>> AddItemAsync(Guid userId, Guid listId, AddShoppingItemDto dto, string lang = "pl");
        Task<ServiceResult<ShoppingItemDto>> AddFromPantryItemAsync(Guid userId, Guid listId, Guid pantryItemId, string lang = "pl");
        Task<ServiceResult<bool>> DeleteItemAsync(Guid userId, Guid listId, Guid itemId);
        Task<ServiceResult<bool>> DeleteItemsAsync(Guid userId, Guid listId, List<Guid> itemIds);
        Task<ServiceResult<bool>> BulkTransferToPantryAsync(Guid userId, Guid listId, BulkTransferToPantryDto dto);
    }
}