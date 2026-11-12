using Granary.Api.Models;
using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Services
{
    public interface IAdminCategoryService
    {
        Task<ServiceResult<IEnumerable<CategoryDto>>> GetAllForAdminAsync();
        Task<ServiceResult<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryDto dto);
        Task<ServiceResult<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryJsonDto dto);
    }

    public class AdminCategoryService : IAdminCategoryService
    {
        private const long MaxImageSizeBytes = 5 * 1024 * 1024;
        private readonly GranaryDbContext _context;
        private readonly IFileStorageService _fileStorageService;

        public AdminCategoryService(GranaryDbContext context, IFileStorageService fileStorageService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
        }

        public async Task<ServiceResult<IEnumerable<CategoryDto>>> GetAllForAdminAsync()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(category => category.DisplayOrder)
                .ThenBy(category => category.Name)
                .Select(category => new CategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    ImageUrl = category.ImageUrl,
                    DisplayOrder = category.DisplayOrder,
                    IsPantryStaple = category.IsPantryStaple
                })
                .ToListAsync();

            return ServiceResult<IEnumerable<CategoryDto>>.Success(categories);
        }

        public Task<ServiceResult<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryJsonDto dto)
        {
            return UpdateAsync(id, new UpdateCategoryDto
            {
                Name = dto.Name,
                DisplayOrder = dto.DisplayOrder,
                ImageUrl = dto.ImageUrl,
                IsPantryStaple = dto.IsPantryStaple
            });
        }

        public async Task<ServiceResult<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryDto dto)
        {
            var name = dto.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            {
                return ServiceResult<CategoryDto>.Failure("Nazwa kategorii jest wymagana i może mieć maksymalnie 100 znaków.");
            }
            if (dto.DisplayOrder < 0)
            {
                return ServiceResult<CategoryDto>.Failure("Kolejność wyświetlania nie może być ujemna.");
            }

            var category = await _context.Categories.FirstOrDefaultAsync(item => item.Id == id);
            if (category == null)
            {
                return ServiceResult<CategoryDto>.Failure("Nie znaleziono kategorii.");
            }

            string? imageUrl;
            var oldImageToDelete = (string?)null;
            if (dto.ImageFile is { Length: > 0 } imageFile)
            {
                if (!imageFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult<CategoryDto>.Failure("Przesłany plik nie jest obrazem.");
                }

                if (imageFile.Length > MaxImageSizeBytes)
                {
                    return ServiceResult<CategoryDto>.Failure("Obraz kategorii może mieć maksymalnie 5 MB.");
                }

                await using var stream = new MemoryStream();
                await imageFile.CopyToAsync(stream);
                var imageData = $"data:{imageFile.ContentType};base64,{Convert.ToBase64String(stream.ToArray())}";
                imageUrl = await _fileStorageService.ProcessImageAsync(imageData, category.ImageUrl, "categories");

                if (imageUrl == imageData || string.IsNullOrWhiteSpace(imageUrl))
                {
                    return ServiceResult<CategoryDto>.Failure("Nie udało się zapisać obrazu kategorii.");
                }
            }
            else if (string.IsNullOrWhiteSpace(dto.ImageUrl))
            {
                imageUrl = await _fileStorageService.ProcessImageAsync(null, category.ImageUrl, "categories");
            }
            else
            {
                if (dto.ImageUrl.Length > 500 ||
                    !Uri.TryCreate(dto.ImageUrl, UriKind.Absolute, out var imageUri) ||
                    (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
                {
                    return ServiceResult<CategoryDto>.Failure("Podaj poprawny adres obrazu zaczynający się od http:// lub https://.");
                }

                imageUrl = dto.ImageUrl;
                if (!string.Equals(category.ImageUrl, imageUrl, StringComparison.Ordinal) &&
                    category.ImageUrl?.Contains("supabase.co", StringComparison.OrdinalIgnoreCase) == true)
                {
                    oldImageToDelete = category.ImageUrl;
                }
            }

            category.Name = name;
            category.DisplayOrder = dto.DisplayOrder;
            category.ImageUrl = imageUrl;
            category.IsPantryStaple = dto.IsPantryStaple;

            await _context.SaveChangesAsync();
            if (oldImageToDelete != null)
            {
                _fileStorageService.DeleteFile(oldImageToDelete);
            }

            return ServiceResult<CategoryDto>.Success(new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                ImageUrl = category.ImageUrl,
                DisplayOrder = category.DisplayOrder,
                IsPantryStaple = category.IsPantryStaple
            });
        }
    }
}
