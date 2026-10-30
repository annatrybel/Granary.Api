using Granary.Api.Models.Dto.Shopping;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IShoppingListService
    {
        Task<ServiceResult<IEnumerable<ShoppingListSummaryDto>>> GetUserListsAsync(string lang = "pl");
        Task<ServiceResult<ShoppingListDetailDto>> GetListDetailsAsync(Guid listId, string lang = "pl");
        Task<ServiceResult<ShoppingListSummaryDto>> CreateListAsync(CreateShoppingListDto dto);
        Task<ServiceResult<bool>> DeleteListAsync(Guid listId);

        Task<ServiceResult<ShoppingItemDto>> AddItemAsync(Guid listId, AddShoppingItemDto dto, string lang = "pl");
        Task<ServiceResult<ShoppingItemDto>> UpdateItemAsync(Guid listId, Guid itemId, UpdateShoppingItemDto dto);
        Task<ServiceResult<bool>> DeleteItemAsync(Guid listId, Guid itemId);
        Task<ServiceResult<bool>> DeleteItemsAsync(Guid listId, List<Guid> itemIds);
        Task<ServiceResult<bool>> BulkTransferToPantryAsync(Guid listId, BulkTransferToPantryDto dto);
        Task<ServiceResult<int>> AddItemsBulkAsync(Guid listId, IEnumerable<AddShoppingItemDto> dtos, string lang = "pl");
    }
}