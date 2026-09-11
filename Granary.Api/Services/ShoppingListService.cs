using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Shopping;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Services.Shopping
{
    public class ShoppingListService : IShoppingListService
    {
        private readonly GranaryDbContext _dbContext;
        private readonly ILogger<ShoppingListService> _logger;

        public ShoppingListService(GranaryDbContext dbContext, ILogger<ShoppingListService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        #region Zarządzanie Listami

        public async Task<ServiceResult<IEnumerable<ShoppingListSummaryDto>>> GetUserListsAsync(Guid userId)
        {
            var lists = await _dbContext.ShoppingLists
                .AsNoTracking()
                .Include(sl => sl.Items)
                .Where(sl => sl.UserId == userId)
                .OrderByDescending(sl => sl.IsMain)
                .ThenBy(sl => sl.Name)
                .Select(sl => new ShoppingListSummaryDto
                {
                    Id = sl.Id,
                    Name = sl.Name,
                    IsMain = sl.IsMain,
                    TotalItemsCount = sl.Items.Count
                })
                .ToListAsync();

            if (!lists.Any())
            {
                var mainList = new ShoppingList
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = "Główna lista",
                    IsMain = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.ShoppingLists.Add(mainList);
                await _dbContext.SaveChangesAsync();

                return await GetUserListsAsync(userId);
            }

            return ServiceResult<IEnumerable<ShoppingListSummaryDto>>.Success(lists);
        }

        public async Task<ServiceResult<ShoppingListDetailDto>> GetListDetailsAsync(Guid userId, Guid listId)
        {
            var list = await _dbContext.ShoppingLists
                .AsNoTracking()
                .Include(sl => sl.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(sl => sl.Id == listId && sl.UserId == userId);

            if (list == null)
            {
                return ServiceResult<ShoppingListDetailDto>.Failure("Wskazana lista zakupów nie istnieje.");
            }

            var dto = new ShoppingListDetailDto
            {
                Id = list.Id,
                Name = list.Name,
                IsMain = list.IsMain,
                TotalItemsCount = list.Items.Count,
                Items = list.Items
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => new ShoppingItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        Name = i.Name,
                        Emoji = i.Product != null ? i.Product.Emoji : "🛒",
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        CreatedAt = i.CreatedAt
                    })
                    .ToList()
            };

            return ServiceResult<ShoppingListDetailDto>.Success(dto);
        }

        public async Task<ServiceResult<ShoppingListSummaryDto>> CreateListAsync(Guid userId, CreateShoppingListDto dto)
        {
            var newList = new ShoppingList
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = dto.Name.Trim(),
                IsMain = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ShoppingLists.Add(newList);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Użytkownik {UserId} stworzył nową listę zakupów '{ListName}'", userId, newList.Name);

            return ServiceResult<ShoppingListSummaryDto>.Success(new ShoppingListSummaryDto
            {
                Id = newList.Id,
                Name = newList.Name,
                IsMain = newList.IsMain,
                TotalItemsCount = 0
            });
        }

        public async Task<ServiceResult<bool>> DeleteListAsync(Guid userId, Guid listId)
        {
            var list = await _dbContext.ShoppingLists.FirstOrDefaultAsync(sl => sl.Id == listId && sl.UserId == userId);

            if (list == null) return ServiceResult<bool>.Failure("Nie znaleziono listy zakupów.");
            if (list.IsMain) return ServiceResult<bool>.Failure("Nie możesz usunąć głównej listy zakupów.");

            _dbContext.ShoppingLists.Remove(list);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Usunięto listę zakupów {ListId}", listId);
            return ServiceResult<bool>.Success(true);
        }

        #endregion

        #region Zarządzanie Pozycjami na Liście

        public async Task<ServiceResult<ShoppingItemDto>> AddItemAsync(Guid userId, Guid listId, AddShoppingItemDto dto)
        {
            var list = await _dbContext.ShoppingLists.FirstOrDefaultAsync(sl => sl.Id == listId && sl.UserId == userId);
            if (list == null) return ServiceResult<ShoppingItemDto>.Failure("Nie znaleziono listy.");

            string itemName;
            string? emoji = "🛒";
            Guid? productId = dto.ProductId;

            if (productId.HasValue)
            {
                var prod = await _dbContext.Products.FindAsync(productId.Value);
                if (prod == null) return ServiceResult<ShoppingItemDto>.Failure("Produkt z bazy nie istnieje.");
                itemName = prod.Name;
                emoji = prod.Emoji;
            }
            else if (!string.IsNullOrWhiteSpace(dto.CustomName))
            {
                itemName = dto.CustomName.Trim();
            }
            else
            {
                return ServiceResult<ShoppingItemDto>.Failure("Podaj nazwę produktu.");
            }

            var item = new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                ShoppingListId = listId,
                ProductId = productId,
                Name = itemName,
                Quantity = dto.Quantity,
                Unit = dto.Unit ?? "szt.",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ShoppingListItems.Add(item);
            await _dbContext.SaveChangesAsync();

            return ServiceResult<ShoppingItemDto>.Success(new ShoppingItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                Name = item.Name,
                Emoji = emoji,
                Quantity = item.Quantity,
                Unit = item.Unit,
                CreatedAt = item.CreatedAt
            });
        }

        public async Task<ServiceResult<ShoppingItemDto>> AddFromPantryItemAsync(Guid userId, Guid listId, Guid pantryItemId)
        {
            var pantryItem = await _dbContext.PantryItems
                .Include(pi => pi.Product)
                .FirstOrDefaultAsync(pi => pi.Id == pantryItemId && pi.UserId == userId);

            if (pantryItem == null) return ServiceResult<ShoppingItemDto>.Failure("Nie znaleziono produktu w spiżarni.");

            var addDto = new AddShoppingItemDto
            {
                ProductId = pantryItem.ProductId,
                Quantity = pantryItem.Quantity,
                Unit = pantryItem.Unit
            };

            return await AddItemAsync(userId, listId, addDto);
        }

        public async Task<ServiceResult<bool>> DeleteItemAsync(Guid userId, Guid listId, Guid itemId)
        {
            var item = await _dbContext.ShoppingListItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.ShoppingListId == listId && i.ShoppingList.UserId == userId);

            if (item == null) return ServiceResult<bool>.Failure("Nie znaleziono pozycji.");

            _dbContext.ShoppingListItems.Remove(item);
            await _dbContext.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<bool>> DeleteItemsAsync(Guid userId, Guid listId, List<Guid> itemIds)
        {
            if (!itemIds.Any()) return ServiceResult<bool>.Failure("Nie wybrano żadnych pozycji do usunięcia.");

            var itemsToDelete = await _dbContext.ShoppingListItems
                .Where(i => itemIds.Contains(i.Id) && i.ShoppingListId == listId && i.ShoppingList.UserId == userId)
                .ToListAsync();

            if (itemsToDelete.Any())
            {
                _dbContext.ShoppingListItems.RemoveRange(itemsToDelete);
                await _dbContext.SaveChangesAsync();
            }

            return ServiceResult<bool>.Success(true);
        }

        #endregion

        #region MASOWE PRZENOSZENIE DO MAGAZYNU (BULK TRANSFER)

        public async Task<ServiceResult<bool>> BulkTransferToPantryAsync(Guid userId, Guid listId, BulkTransferToPantryDto dto)
        {
            if (!dto.ItemIds.Any()) return ServiceResult<bool>.Failure("Nie zaznaczono żadnych produktów.");

            var itemsToTransfer = await _dbContext.ShoppingListItems
                .Include(i => i.Product)
                .Where(i => dto.ItemIds.Contains(i.Id) && i.ShoppingListId == listId && i.ShoppingList.UserId == userId)
                .ToListAsync();

            if (!itemsToTransfer.Any()) return ServiceResult<bool>.Failure("Nie znaleziono wybranych pozycji.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            foreach (var item in itemsToTransfer)
            {
                var shelfLifeDays = item.Product?.DefaultShelfLifeDays ?? 7;
                var expiryDate = dto.DefaultExpirationDate ?? today.AddDays(shelfLifeDays);

                var pantryItem = new PantryItem
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ProductId = item.ProductId ?? (await GetOrCreateCustomProduct(userId, item.Name, item.Unit)),
                    StorageLocation = dto.DestinationLocation, 
                    Status = PantryItemStatus.Active,
                    Quantity = item.Quantity,
                    Unit = item.Unit ?? "szt.",
                    PurchaseDate = today,
                    ExpirationDate = expiryDate,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.PantryItems.Add(pantryItem);
            }

            _dbContext.ShoppingListItems.RemoveRange(itemsToTransfer);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Przeniesiono masowo {Count} produktów z listy zakupów do {Location}", itemsToTransfer.Count, dto.DestinationLocation);

            return ServiceResult<bool>.Success(true);
        }

        private async Task<Guid> GetOrCreateCustomProduct(Guid userId, string name, string? unit)
        {
            var existing = await _dbContext.Products.FirstOrDefaultAsync(p => p.CreatedByUserId == userId && p.Name.ToLower() == name.ToLower());
            if (existing != null) return existing.Id;

            var defaultCat = await _dbContext.Categories.OrderBy(c => c.DisplayOrder).FirstAsync();
            var newProd = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                CategoryId = defaultCat.Id,
                Emoji = "📦",
                DefaultUnit = unit ?? "szt.",
                CreatedByUserId = userId
            };
            _dbContext.Products.Add(newProd);
            await _dbContext.SaveChangesAsync();
            return newProd.Id;
        }

        #endregion
    }
}