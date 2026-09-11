using Granary.Api.Models.Dto;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface ICatalogProductService
    {
        Task<ServiceResult<IEnumerable<CatalogProductDto>>> SearchAsync(string? query, Guid? categoryId, Guid? currentUserId = null);
        Task<ServiceResult<CatalogProductDto>> GetByIdAsync(Guid id);
        Task<ServiceResult<CatalogProductDto>> GetByBarcodeAsync(string barcode);
        Task<ServiceResult<CatalogProductDto>> CreateAsync(CreateOrUpdateCatalogProductDto createDto, Guid? currentUserId = null);
        Task<ServiceResult<CatalogProductDto>> UpdateAsync(Guid id, CreateOrUpdateCatalogProductDto updateDto, Guid? currentUserId = null, bool isAdmin = false);
        Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid? currentUserId = null, bool isAdmin = false);
    }
}