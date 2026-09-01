using Granary.Api.Models.Dto;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IProductService
    {
        Task<ServiceResult<IEnumerable<ProductDto>>> GetAllAsync(Guid? currentUserId = null);
        Task<ServiceResult<ProductDto>> GetByIdAsync(Guid id);
        Task<ServiceResult<ProductDto>> GetByBarcodeAsync(string barcode);
        Task<ServiceResult<ProductDto>> CreateAsync(CreateOrUpdateProductDto createDto, Guid? currentUserId = null);
        Task<ServiceResult<ProductDto>> UpdateAsync(Guid id, CreateOrUpdateProductDto updateDto);
        Task<ServiceResult<bool>> DeleteAsync(Guid id);
    }
}
