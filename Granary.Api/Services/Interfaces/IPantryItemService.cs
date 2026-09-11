using Granary.Api.Models.Dto;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IPantryItemService
    {
        Task<ServiceResult<IEnumerable<PantryItemDto>>> GetUserItemsAsync(Guid userId, StorageLocation? location = null);
        Task<ServiceResult<PantryItemDto>> GetByIdAsync(Guid userId, Guid pantryItemId);
        Task<ServiceResult<PantryItemDto>> AddItemAsync(Guid userId, CreateOrUpdatePantryItemDto dto);
        Task<ServiceResult<PantryItemDto>> UpdateItemAsync(Guid userId, Guid pantryItemId, CreateOrUpdatePantryItemDto dto);
        Task<ServiceResult<bool>> ChangeStatusAsync(Guid userId, Guid pantryItemId, PantryItemStatus newStatus);
        Task<ServiceResult<bool>> DeleteItemAsync(Guid userId, Guid pantryItemId);
    }
}