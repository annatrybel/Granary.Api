using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Pantry;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;

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
                .Select(pi => new PantryItemDto
                {
                    Id = pi.Id,
                    ProductId = pi.ProductId,
                    ProductName = pi.Product.Translations
                        .Where(t => t.Language.Code == lang)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? pi.Product.Name,
                    ImageUrl = pi.Product.ImageUrl,
                    CategoryName = pi.Product.Category.Translations
                        .Where(t => t.Language.Code == lang)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? pi.Product.Category.Name,
                    StorageLocation = pi.StorageLocation,
                    Status = pi.Status,
                    Quantity = pi.Quantity,
                    Unit = pi.Unit,
                    PurchaseDate = pi.PurchaseDate,
                    ExpirationDate = pi.ExpirationDate
                })
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
                .Select(pi => new PantryItemDto
                {
                    Id = pi.Id,
                    ProductId = pi.ProductId,
                    ProductName = pi.Product.Translations
                        .Where(t => t.Language.Code == lang)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? pi.Product.Name,
                    ImageUrl = pi.Product.ImageUrl,
                    CategoryName = pi.Product.Category.Translations
                        .Where(t => t.Language.Code == lang)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? pi.Product.Category.Name,
                    StorageLocation = pi.StorageLocation,
                    Status = pi.Status,
                    Quantity = pi.Quantity,
                    Unit = pi.Unit,
                    PurchaseDate = pi.PurchaseDate,
                    ExpirationDate = pi.ExpirationDate
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return ServiceResult<PantryItemDto>.Failure("Nie znaleziono produktu.");
            }

            return ServiceResult<PantryItemDto>.Success(item);
        }

        public async Task<ServiceResult<PantryItemDto>> AddItemAsync(
            CreateOrUpdatePantryItemDto dto,
            string lang = "pl")
        {
            var userId = _currentUserService.UserId;
            Guid targetProductId;

            if (dto.ProductId.HasValue && dto.ProductId.Value != Guid.Empty)
            {
                var productExists = await _dbContext.Products.AnyAsync(p => p.Id == dto.ProductId.Value);
                if (!productExists)
                {
                    return ServiceResult<PantryItemDto>.Failure("Wybrany produkt nie istnieje.");
                }
                targetProductId = dto.ProductId.Value;
            }
            else if (!string.IsNullOrWhiteSpace(dto.CustomProductName))
            {
                var trimmedName = dto.CustomProductName.Trim();
                var existingCustom = await _dbContext.Products.FirstOrDefaultAsync(p =>
                    p.CreatedByUserId == userId && p.Name.ToLower() == trimmedName.ToLower());

                if (existingCustom != null)
                {
                    targetProductId = existingCustom.Id;
                }
                else
                {
                    Guid categoryId;
                    if (dto.CustomCategoryId.HasValue && dto.CustomCategoryId.Value != Guid.Empty)
                    {
                        categoryId = dto.CustomCategoryId.Value;
                    }
                    else
                    {
                        var defaultCat = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name == "Inne")
                                      ?? await _dbContext.Categories.FirstOrDefaultAsync();
                        categoryId = defaultCat?.Id ?? Guid.NewGuid();
                    }

                    string? persistentImageUrl = await _fileStorageService.ProcessImageAsync(dto.CustomImageUrl, null, "products");

                    var newCustomProduct = new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = trimmedName,
                        CategoryId = categoryId,
                        ImageUrl = persistentImageUrl,
                        DefaultUnit = !string.IsNullOrWhiteSpace(dto.Unit) ? dto.Unit : "szt.",
                        CreatedByUserId = userId
                    };

                    _dbContext.Products.Add(newCustomProduct);
                    targetProductId = newCustomProduct.Id;
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
                if (dto.ExpirationDate != default)
                {
                    existingItem.ExpirationDate = dto.ExpirationDate;
                }

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
                Unit = !string.IsNullOrWhiteSpace(dto.Unit) ? dto.Unit : "szt.",
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

            if (item == null)
            {
                return ServiceResult<PantryItemDto>.Failure("Produkt nie istnieje w Twojej spiżarni.");
            }

            if (item.UserId != userId)
            {
                return ServiceResult<PantryItemDto>.Failure("Brak uprawnień do edycji tego produktu.");
            }

            item.Quantity = dto.Quantity > 0 ? dto.Quantity : 1;
            item.ExpirationDate = dto.ExpirationDate;
            item.StorageLocation = dto.Location;

            if (dto.PurchaseDate != default)
            {
                item.PurchaseDate = dto.PurchaseDate;
            }

            if (!string.IsNullOrWhiteSpace(dto.Unit))
            {
                item.Unit = dto.Unit.Trim();
            }
                       
            if (item.Product.CreatedByUserId == userId)
            {
                if (dto.CustomCategoryId.HasValue && dto.CustomCategoryId.Value != Guid.Empty)
                {
                    item.Product.CategoryId = dto.CustomCategoryId.Value;
                }

                if (!string.IsNullOrWhiteSpace(dto.CustomProductName))
                {
                    var trimmedNewName = dto.CustomProductName.Trim();

                    if (!string.Equals(item.Product.Name, trimmedNewName, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Product.Name = trimmedNewName;

                        if (item.Product.Translations.Any())
                        {
                            _dbContext.ProductTranslations.RemoveRange(item.Product.Translations);
                            _logger.LogInformation("Zresetowano stare tłumaczenia dla zaktualizowanego produktu {ProductId}", item.Product.Id);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(dto.Unit))
                {
                    item.Product.DefaultUnit = dto.Unit.Trim();
                }

                if (dto.CustomImageUrl != item.Product.ImageUrl)
                {
                    item.Product.ImageUrl = await _fileStorageService.ProcessImageAsync(
                        dto.CustomImageUrl,
                        item.Product.ImageUrl,
                        "products");
                }
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Użytkownik {UserId} zaktualizował produkt {PantryItemId} (Produkt: {ProductId})",
                userId, pantryItemId, item.ProductId);

            return await GetByIdAsync(item.Id, lang);
        }

        public async Task<ServiceResult<bool>> DeleteItemAsync(Guid pantryItemId)
        {
            var userId = _currentUserService.UserId;
            var item = await _dbContext.PantryItems
                .Include(pi => pi.Product)
                .FirstOrDefaultAsync(pi => pi.Id == pantryItemId && pi.UserId == userId);

            if (item == null)
            {
                return ServiceResult<bool>.Failure("Nie znaleziono produktu.");
            }

            var productId = item.ProductId;
            var isCustomProduct = item.Product.CreatedByUserId == userId;

            _dbContext.PantryItems.Remove(item);
            await _dbContext.SaveChangesAsync();

            if (isCustomProduct)
            {
                var isUsedElsewhere = await _dbContext.PantryItems.AnyAsync(pi => pi.ProductId == productId)
                                   || await _dbContext.RecipeIngredients.AnyAsync(ri => ri.ProductId == productId);

                if (!isUsedElsewhere)
                {
                    var customProduct = await _dbContext.Products.FindAsync(productId);
                    if (customProduct != null)
                    {
                        if (!string.IsNullOrWhiteSpace(customProduct.ImageUrl))
                        {
                            _fileStorageService.DeleteFile(customProduct.ImageUrl);
                        }

                        _dbContext.Products.Remove(customProduct);
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("Usunięto nieużywany produkt własny {ProductId} oraz jego zdjęcie z bazy i storage.", productId);
                    }
                }
            }

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<IEnumerable<ExpiringPantryItemDto>>> GetExpiringItemsAsync(
            string lang = "pl",
            int days = 4,
            int limit = 10)
        {
            var userId = _currentUserService.UserId;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var maxDate = today.AddDays(days);

            var items = await _dbContext.PantryItems
                .AsNoTracking()
                .Where(pi => pi.UserId == userId
                          && pi.Status == PantryItemStatus.Active
                          && pi.ExpirationDate != default
                          && pi.ExpirationDate <= maxDate)
                .OrderBy(pi => pi.ExpirationDate)
                .Take(limit)
                .Select(pi => new ExpiringPantryItemDto
                {
                    Id = pi.Id,
                    ProductName = pi.Product.Translations
                        .Where(t => t.Language.Code == lang)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? pi.Product.Name,
                    ProductImageUrl = pi.Product.ImageUrl,
                    CategoryName = pi.Product.Category.Translations
                        .Where(t => t.Language.Code == lang)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? pi.Product.Category.Name,
                    CategoryImageUrl = pi.Product.Category.ImageUrl,
                    StorageLocation = pi.StorageLocation,
                    ExpirationDate = pi.ExpirationDate,
                    DaysLeft = pi.ExpirationDate.DayNumber - today.DayNumber
                })
                .ToListAsync();

            return ServiceResult<IEnumerable<ExpiringPantryItemDto>>.Success(items);
        }
    }
}