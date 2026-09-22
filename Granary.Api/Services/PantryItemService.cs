using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Services
{
    public class PantryItemService : IPantryItemService
    {
        private readonly GranaryDbContext _dbContext;
        private readonly ILogger<PantryItemService> _logger;

        public PantryItemService(GranaryDbContext dbContext, ILogger<PantryItemService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<ServiceResult<IEnumerable<PantryItemDto>>> GetUserItemsAsync(Guid userId, StorageLocation? location = null)
        {
            var query = _dbContext.PantryItems
                .AsNoTracking()
                .Include(pi => pi.Product)
                    .ThenInclude(p => p.Category)
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
                    ProductName = pi.Product.Name,
                    ImageUrl = pi.Product.ImageUrl, 
                    CategoryName = pi.Product.Category.Name,
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

        public async Task<ServiceResult<PantryItemDto>> AddItemAsync(Guid userId, CreateOrUpdatePantryItemDto dto)
        {
            Guid targetProductId;

            // 1. SCENARIUSZ: PRODUKT Z KATALOGU
            if (dto.ProductId.HasValue && dto.ProductId.Value != Guid.Empty)
            {
                var productExists = await _dbContext.Products.AnyAsync(p => p.Id == dto.ProductId.Value);
                if (!productExists)
                {
                    return ServiceResult<PantryItemDto>.Failure("Wybrany produkt z katalogu nie istnieje.");
                }
                targetProductId = dto.ProductId.Value;
            }
            // 2. SCENARIUSZ: WŁASNY PRODUKT (CUSTOM)
            else if (!string.IsNullOrWhiteSpace(dto.CustomProductName))
            {
                var trimmedName = dto.CustomProductName.Trim();

                var existingCustom = await _dbContext.Products.FirstOrDefaultAsync(p =>
                    p.CreatedByUserId == userId &&
                    p.Name.ToLower() == trimmedName.ToLower());

                if (existingCustom != null)
                {
                    targetProductId = existingCustom.Id;
                }
                else
                {
                    // Jeśli nie wybrano kategorii, bierzemy kategorię "Inne" lub pierwszą lepszą z bazy:
                    Guid categoryId;
                    if (dto.CustomCategoryId.HasValue && dto.CustomCategoryId.Value != Guid.Empty)
                    {
                        categoryId = dto.CustomCategoryId.Value;
                    }
                    else
                    {
                        var defaultCategory = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name == "Inne")
                                           ?? await _dbContext.Categories.FirstOrDefaultAsync();

                        if (defaultCategory == null)
                        {
                            return ServiceResult<PantryItemDto>.Failure("Brak kategorii w bazie. Skonfiguruj kategorie.");
                        }
                        categoryId = defaultCategory.Id;
                    }

                    var newCustomProduct = new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = trimmedName,
                        CategoryId = categoryId,
                        ImageUrl = dto.CustomImageUrl, 
                        DefaultUnit = !string.IsNullOrWhiteSpace(dto.Unit) ? dto.Unit : "szt.",
                        CreatedByUserId = userId
                    };

                    _dbContext.Products.Add(newCustomProduct);
                    targetProductId = newCustomProduct.Id;
                }
            }
            else
            {
                return ServiceResult<PantryItemDto>.Failure("Musisz wybrać produkt z katalogu lub podać nazwę własnego produktu.");
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

            _logger.LogInformation("Użytkownik {UserId} dodał produkt do {Location}", userId, dto.Location);

            return await GetByIdAsync(userId, pantryItem.Id);
        }

        public async Task<ServiceResult<PantryItemDto>> UpdateItemAsync(Guid userId, Guid pantryItemId, CreateOrUpdatePantryItemDto dto)
        {
            var item = await _dbContext.PantryItems
                .Include(pi => pi.Product)
                .FirstOrDefaultAsync(pi => pi.Id == pantryItemId);

            if (item == null)
            {
                return ServiceResult<PantryItemDto>.Failure("Nie znaleziono wskazanego produktu w Twojej spiżarni.");
            }

            if (item.UserId != userId)
            {
                return ServiceResult<PantryItemDto>.Failure("Brak uprawnień do edycji tego produktu.");
            }

            item.Quantity = dto.Quantity;
            item.ExpirationDate = dto.ExpirationDate;
            item.StorageLocation = dto.Location;
            if (dto.PurchaseDate != default) item.PurchaseDate = dto.PurchaseDate;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Użytkownik {UserId} zaktualizował produkt {PantryItemId} w spiżarni", userId, pantryItemId);

            return await GetByIdAsync(userId, item.Id);
        }

        public async Task<ServiceResult<bool>> ChangeStatusAsync(Guid userId, Guid pantryItemId, PantryItemStatus newStatus)
        {
            var item = await _dbContext.PantryItems.FirstOrDefaultAsync(pi => pi.Id == pantryItemId);

            if (item == null)
            {
                return ServiceResult<bool>.Failure("Nie znaleziono wskazanego produktu w Twojej spiżarni.");
            }

            if (item.UserId != userId)
            {
                return ServiceResult<bool>.Failure("Brak uprawnień do zmiany statusu tego produktu.");
            }

            item.Status = newStatus;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Użytkownik {UserId} zmienił status produktu {PantryItemId} na {Status}", userId, pantryItemId, newStatus);

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<bool>> DeleteItemAsync(Guid userId, Guid pantryItemId)
        {
            var item = await _dbContext.PantryItems
                .Include(pi => pi.Product)
                .FirstOrDefaultAsync(pi => pi.Id == pantryItemId && pi.UserId == userId);

            if (item == null)
            {
                return ServiceResult<bool>.Failure("Nie znaleziono wskazanego produktu w Twojej spiżarni.");
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
                        _dbContext.Products.Remove(customProduct);
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("Automatycznie usunięto nieużywany customowy produkt {ProductId} z bazy.", productId);
                    }
                }
            }

            _logger.LogInformation("Użytkownik {UserId} usunął produkt {PantryItemId} ze spiżarni", userId, pantryItemId);

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<PantryItemDto>> GetByIdAsync(Guid userId, Guid pantryItemId)
        {
            var item = await _dbContext.PantryItems
                .AsNoTracking()
                .Include(pi => pi.Product)
                    .ThenInclude(p => p.Category)
                .Where(pi => pi.Id == pantryItemId && pi.UserId == userId)
                .Select(pi => new PantryItemDto
                {
                    Id = pi.Id,
                    ProductId = pi.ProductId,
                    ProductName = pi.Product.Name,
                    ImageUrl = pi.Product.ImageUrl,
                    CategoryName = pi.Product.Category.Name,
                    StorageLocation = pi.StorageLocation,
                    Status = pi.Status,
                    Quantity = pi.Quantity,
                    Unit = pi.Unit,
                    PurchaseDate = pi.PurchaseDate,
                    ExpirationDate = pi.ExpirationDate
                })
                .FirstOrDefaultAsync();

            if (item == null) return ServiceResult<PantryItemDto>.Failure("Nie znaleziono produktu.");
            return ServiceResult<PantryItemDto>.Success(item);
        }
    }
}