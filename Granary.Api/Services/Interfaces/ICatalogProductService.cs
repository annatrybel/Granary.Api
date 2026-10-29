using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface ICatalogProductService
    {
        Task<ServiceResult<IEnumerable<CatalogProductDto>>> SearchAsync(string? query, Guid? categoryId, bool excludePantry = false, string lang = "pl");
        Task<ServiceResult<CatalogProductDto>> GetByIdAsync(Guid id, string lang = "pl");
        Task<ServiceResult<CatalogProductDto>> CreateAsync(CreateOrUpdateCatalogProductDto createDto, Guid? currentUserId = null);
        Task<ServiceResult<CatalogProductDto>> UpdateAsync(Guid id, CreateOrUpdateCatalogProductDto updateDto, Guid? currentUserId = null, bool isAdmin = false);
        Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid? currentUserId = null, bool isAdmin = false);
        Task<ServiceResult<IEnumerable<CategoryDto>>> GetCategoriesAsync(string lang = "pl");
        Task<Guid> GetOrCreateCustomProductAsync(string name, Guid? categoryId = null, string? unit = null, string? imageUrl = null);
    }
}