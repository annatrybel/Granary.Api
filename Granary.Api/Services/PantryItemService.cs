using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Pantry;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Auth;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Granary.Api.Services
{
    public class PantryItemService : IPantryItemService
    {
        private readonly GranaryDbContext _dbContext;
        private readonly ILogger<PantryItemService> _logger;
        private readonly IFileStorageService _fileStorageService;
        private readonly ICurrentUserService _currentUserService;

        public PantryItemService(
            GranaryDbContext dbContext,
            ILogger<PantryItemService> logger,
            IFileStorageService fileStorageService,
            ICurrentUserService currentUserService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _fileStorageService = fileStorageService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResult<IEnumerable<PantryItemDto>>> GetUserItemsAsync(
            string lang = "pl",
            StorageLocation? location = null)
        {
            var userId = _currentUserService.UserId;
            var query = _dbContext.PantryItems
                .AsNoTracking()
                .Where(pi => pi.UserId == userId && pi.Status == PantryItemStatus.Active);

            if (location.HasValue)
            {
                query = query.Where(pi => pi.StorageLocation == location.Value);
            }

            var items = await query
                .Select(ProjectToDto(userId, lang))
                .ToListAsync();

            return ServiceResult<IEnumerable<PantryItemDto>>.Success(items);
        }

        public async Task<ServiceResult<PantryItemDto>> GetByIdAsync(
            Guid pantryItemId,
            string lang = "pl")
        {
            var userId = _currentUserService.UserId;
            var item = await _dbContext.PantryItems
                .AsNoTracking()
                .Where(pi => pi.Id == pantryItemId && pi.UserId == userId)
                .Select(ProjectToDto(userId, lang))
                .FirstOrDefaultAsync();

            return item != null
                ? ServiceResult<PantryItemDto>.Success(item)
                : ServiceResult<PantryItemDto>.Failure("Nie znaleziono produktu.");
        }

        public async Task<ServiceResult<PantryItemDto>> AddItemAsync(
            CreateOrUpdatePantryItemDto dto,
            string lang = "pl")
        {
            var userId = _currentUserService.UserId;
            Guid targetProductId;

            if (dto.ProductId is { } pId && pId != Guid.Empty)
            {
                if (!await _dbContext.Products.AnyAsync(p => p.Id == pId))
                    return ServiceResult<PantryItemDto>.Failure("Wybrany produkt nie istnieje.");

                targetProductId = pId;
            }
            else if (!string.IsNullOrWhiteSpace(dto.CustomProductName))
            {
                var trimmedName = dto.CustomProductName.Trim();
                var normalizedName = trimmedName.ToLower();

                var existingSystem = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.CreatedByUserId == null && p.Name.ToLower() == normalizedName);

                if (existingSystem != null)
                {
                    targetProductId = existingSystem.Id;
                }
                else
                {
                    var existingCustom = await _dbContext.Products.FirstOrDefaultAsync(p =>
                        p.CreatedByUserId == userId && p.Name.ToLower() == normalizedName);

                    if (existingCustom != null)
                    {
                        targetProductId = existingCustom.Id;
                    }
                    else
                    {
                        var categoryId = dto.CustomCategoryId is { } catId && catId != Guid.Empty
                            ? catId
                            : (await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name == "Inne")
                               ?? await _dbContext.Categories.FirstAsync()).Id;

                        var newProduct = new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = trimmedName,
                            CategoryId = categoryId,
                            ImageUrl = await _fileStorageService.ProcessImageAsync(dto.CustomImageUrl, null, "products"),
                            Unit = !string.IsNullOrWhiteSpace(dto.Unit) ? dto.Unit : "szt.",
                            CreatedByUserId = userId,
                            CreatedAt = DateTime.UtcNow
                        };

                        _dbContext.Products.Add(newProduct);
                        await _dbContext.SaveChangesAsync();
                        targetProductId = newProduct.Id;
                    }
                }
            }
            else
            {
                return ServiceResult<PantryItemDto>.Failure("Wybierz produkt z katalogu lub podaj własną nazwę.");
            }

            var existingItem = await _dbContext.PantryItems
                .FirstOrDefaultAsync(pi => pi.UserId == userId
                                        && pi.ProductId == targetProductId
                                        && pi.StorageLocation == dto.Location
                                        && pi.Status == PantryItemStatus.Active);

            if (existingItem != null)
            {
                existingItem.Quantity += dto.Quantity > 0 ? dto.Quantity : 1;
                if (dto.ExpirationDate != default) existingItem.ExpirationDate = dto.ExpirationDate;

                await _dbContext.SaveChangesAsync();
                return await GetByIdAsync(existingItem.Id, lang);
            }

            var pantryItem = new PantryItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProductId = targetProductId,
                StorageLocation = dto.Location,
                Status = PantryItemStatus.Active,
                Quantity = dto.Quantity > 0 ? dto.Quantity : 1,
                PurchaseDate = dto.PurchaseDate != default ? dto.PurchaseDate : DateOnly.FromDateTime(DateTime.UtcNow),
                ExpirationDate = dto.ExpirationDate,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.PantryItems.Add(pantryItem);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(pantryItem.Id, lang);
        }

        public async Task<ServiceResult<PantryItemDto>> UpdateItemAsync(
            Guid pantryItemId,
            CreateOrUpdatePantryItemDto dto,
            string lang = "pl")
        {
            var userId = _currentUserService.UserId;
            var item = await _dbContext.PantryItems
                .Include(pi => pi.Product)
                    .ThenInclude(p => p.Translations)
                .FirstOrDefaultAsync(pi => pi.Id == pantryItemId);

            if (item == null) return ServiceResult<PantryItemDto>.Failure("Produkt nie istnieje w Twojej spiżarni.");
            if (item.UserId != userId) return ServiceResult<PantryItemDto>.Failure("Brak uprawnień do edycji tego produktu.");

            item.Quantity = dto.Quantity > 0 ? dto.Quantity : 1;
            item.ExpirationDate = dto.ExpirationDate;
            item.StorageLocation = dto.Location;
            if (dto.PurchaseDate != default) item.PurchaseDate = dto.PurchaseDate;

            if (item.Product.CreatedByUserId == userId)
            {
                if (dto.CustomCategoryId is { } catId && catId != Guid.Empty)
                    item.Product.CategoryId = catId;

                if (!string.IsNullOrWhiteSpace(dto.CustomProductName))
                {
                    var trimmedNewName = dto.CustomProductName.Trim();
                    if (!string.Equals(item.Product.Name, trimmedNewName, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Product.Name = trimmedNewName;
                        if (item.Product.Translations.Any())
                            _dbContext.ProductTranslations.RemoveRange(item.Product.Translations);
                    }
                }

                if (!string.IsNullOrWhiteSpace(dto.Unit))
                    item.Product.Unit = dto.Unit.Trim();

                if (dto.CustomImageUrl != item.Product.ImageUrl)
                {
                    item.Product.ImageUrl = await _fileStorageService.ProcessImageAsync(
                        dto.CustomImageUrl,
                        item.Product.ImageUrl,
                        "products");
                }
            }

            await _dbContext.SaveChangesAsync();
            return await GetByIdAsync(item.Id, lang);
        }

        public async Task<ServiceResult<bool>> DeleteItemAsync(Guid pantryItemId)
        {
            var userId = _currentUserService.UserId;
            var item = await _dbContext.PantryItems
                .Include(pi => pi.Product)
                .FirstOrDefaultAsync(pi => pi.Id == pantryItemId && pi.UserId == userId);

            if (item == null) return ServiceResult<bool>.Failure("Nie znaleziono produktu.");

            var productId = item.ProductId;
            var isCustomProduct = item.Product.CreatedByUserId == userId;
            var imageUrl = item.Product.ImageUrl;
            var productRef = item.Product;

            _dbContext.PantryItems.Remove(item);
            await _dbContext.SaveChangesAsync();

            if (isCustomProduct)
            {
                var isUsedElsewhere = await _dbContext.PantryItems.AnyAsync(pi => pi.ProductId == productId)
                                   || await _dbContext.RecipeIngredients.AnyAsync(ri => ri.ProductId == productId)
                                   || await _dbContext.ShoppingListItems.AnyAsync(sli => sli.ProductId == productId);

                if (!isUsedElsewhere)
                {
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                        _fileStorageService.DeleteFile(imageUrl);

                    _dbContext.Products.Remove(productRef);
                    await _dbContext.SaveChangesAsync();
                }
            }

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<bool>> ResolveItemAsync(Guid pantryItemId, PantryItemStatus status)
        {
            if (status is not (PantryItemStatus.Consumed or PantryItemStatus.Wasted))
                return ServiceResult<bool>.Failure("Nieprawidłowy status produktu.");

            var userId = _currentUserService.UserId;
            var item = await _dbContext.PantryItems
                .FirstOrDefaultAsync(pi => pi.Id == pantryItemId && pi.UserId == userId);

            if (item == null) return ServiceResult<bool>.Failure("Nie znaleziono produktu.");

            item.Status = status;
            item.ResolvedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<PantryZeroWasteStatsDto>> GetZeroWasteStatsAsync()
        {
            var userId = _currentUserService.UserId;
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var stats = await _dbContext.PantryItems
                .AsNoTracking()
                .Where(pi => pi.UserId == userId && pi.ResolvedAt >= startOfMonth)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Consumed = g.Count(pi => pi.Status == PantryItemStatus.Consumed),
                    Wasted = g.Count(pi => pi.Status == PantryItemStatus.Wasted)
                })
                .FirstOrDefaultAsync();

            var consumed = stats?.Consumed ?? 0;
            var wasted = stats?.Wasted ?? 0;
            var total = consumed + wasted;

            return ServiceResult<PantryZeroWasteStatsDto>.Success(new PantryZeroWasteStatsDto
            {
                ConsumedCount = consumed,
                WastedCount = wasted,
                Percentage = total == 0 ? 100m : Math.Round(consumed * 100m / total, 2)
            });
        }

        #region Helpers

        private static Expression<Func<PantryItem, PantryItemDto>> ProjectToDto(Guid userId, string lang) =>
            pi => new PantryItemDto
            {
                Id = pi.Id,
                ProductId = pi.ProductId,
                IsCustom = pi.Product.CreatedByUserId == userId,
                ProductName = pi.Product.Translations
                    .Where(t => t.Language.Code == lang)
                    .Select(t => t.Name)
                    .FirstOrDefault() ?? pi.Product.Name,
                ImageUrl = pi.Product.ImageUrl,
                CategoryName = pi.Product.Category.Translations
                    .Where(t => t.Language.Code == lang)
                    .Select(t => t.Name)
                    .FirstOrDefault() ?? pi.Product.Category.Name,
                CategoryImageUrl = pi.Product.Category.ImageUrl,
                StorageLocation = pi.StorageLocation,
                Status = pi.Status,
                Quantity = pi.Quantity,
                Unit = pi.Product.Unit,
                PurchaseDate = pi.PurchaseDate,
                ExpirationDate = pi.ExpirationDate
            };

        #endregion
    }
}