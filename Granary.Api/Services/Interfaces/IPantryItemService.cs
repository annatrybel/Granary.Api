using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Pantry;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IPantryItemService
    {
        Task<ServiceResult<IEnumerable<PantryItemDto>>> GetUserItemsAsync(Guid userId, string lang = "pl", StorageLocation? location = null);
        Task<ServiceResult<PantryItemDto>> GetByIdAsync(Guid userId, Guid pantryItemId, string lang = "pl");
        Task<ServiceResult<PantryItemDto>> AddItemAsync(Guid userId, CreateOrUpdatePantryItemDto dto, string lang = "pl");
        Task<ServiceResult<PantryItemDto>> UpdateItemAsync(Guid userId, Guid pantryItemId, CreateOrUpdatePantryItemDto dto, string lang = "pl");
        Task<ServiceResult<bool>> DeleteItemAsync(Guid userId, Guid pantryItemId);
        Task<ServiceResult<IEnumerable<ExpiringPantryItemDto>>> GetExpiringItemsAsync(Guid userId, string lang = "pl", int days = 4, int limit = 10);
    }
}