using Granary.Api.Extensions;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Auth;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Linq.Expressions;

namespace Granary.Api.Services.Catalog
{
    public class CatalogProductService : ICatalogProductService
    {
        private readonly GranaryDbContext _dbContext;
        private readonly ILogger<CatalogProductService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMemoryCache _cache;
        private readonly IFileStorageService _fileStorageService;

        public CatalogProductService(
            GranaryDbContext dbContext,
            ILogger<CatalogProductService> logger,
            ICurrentUserService currentUserService,
            IMemoryCache cache,
            IFileStorageService fileStorageService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _currentUserService = currentUserService;
            _cache = cache;
            _fileStorageService = fileStorageService;
        }

        private static Expression<Func<Product, CatalogProductDto>> CreateDtoSelector(Guid targetLangId) => p => new CatalogProductDto
        {
            Id = p.Id,
            Name = p.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? p.Name,
            ImageUrl = p.ImageUrl,
            DefaultUnit = p.Translations.Where(t => t.LanguageId == targetLangId && t.Unit != null).Select(t => t.Unit).FirstOrDefault() ?? p.Unit,
            DefaultShelfLifeDays = p.DefaultShelfLifeDays ?? 7,
            CategoryId = p.CategoryId,
            CategoryName = p.Category.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? p.Category.Name,
            CategoryImageUrl = p.Category.ImageUrl,
            CreatedByUserId = p.CreatedByUserId
        };

        public async Task<ServiceResult<IEnumerable<CatalogProductDto>>> SearchAsync(
            string? query,
            Guid? categoryId,
            bool excludePantry = false,
            string lang = "pl")
        {
            var userId = _currentUserService.UserId;
            var normalizedLang = (lang ?? "pl").Trim().ToLowerInvariant();

            var targetLangId = await _cache.GetLanguageIdAsync(
                _dbContext, $"lang:catalog:{normalizedLang}",
                l => l.Code.ToLower() == normalizedLang || l.Code.ToLower().StartsWith(normalizedLang));

            var dbQuery = _dbContext.Products
                .AsNoTracking()
                .Where(p => p.CreatedByUserId == null || p.CreatedByUserId == userId);

            if (excludePantry)
            {
                dbQuery = dbQuery.Where(p => !_dbContext.PantryItems.Any(pi =>
                    pi.UserId == userId && pi.Status == PantryItemStatus.Active && pi.ProductId == p.Id));
            }

            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                dbQuery = dbQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var pattern = query.Trim().ToContainsPattern();
                dbQuery = dbQuery.Where(p =>
                    EF.Functions.ILike(p.Name, pattern) ||
                    p.Translations.Any(t => t.LanguageId == targetLangId && EF.Functions.ILike(t.Name, pattern))
                );
            }

            var results = await dbQuery
                .OrderBy(p => p.Name)
                .Take(50)
                .Select(CreateDtoSelector(targetLangId))
                .ToListAsync();

            return ServiceResult<IEnumerable<CatalogProductDto>>.Success(results);
        }

        public async Task<ServiceResult<AdminCatalogProductsPageDto>> SearchForAdminAsync(AdminCatalogProductsQueryDto query, string lang = "pl")
        {
            const int maxPageSize = 100;
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, maxPageSize);

            var normalizedLang = (lang ?? "pl").Trim().ToLowerInvariant();
            var targetLangId = await _cache.GetLanguageIdAsync(
                _dbContext, $"lang:catalog:{normalizedLang}",
                l => l.Code.ToLower() == normalizedLang || l.Code.ToLower().StartsWith(normalizedLang));

            var products = _dbContext.Products.AsNoTracking();

            if (query.IsCustom.HasValue)
            {
                products = query.IsCustom.Value
                    ? products.Where(p => p.CreatedByUserId != null)
                    : products.Where(p => p.CreatedByUserId == null);
            }

            if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty)
            {
                products = products.Where(p => p.CategoryId == query.CategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = query.Search.Trim().ToContainsPattern();
                products = products.Where(p =>
                    EF.Functions.ILike(p.Name, pattern) ||
                    p.Translations.Any(t => EF.Functions.ILike(t.Name, pattern)));
            }

            var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var ordered = (query.SortBy?.Trim().ToLowerInvariant()) switch
            {
                "shelflife" => descending ? products.OrderByDescending(p => p.DefaultShelfLifeDays) : products.OrderBy(p => p.DefaultShelfLifeDays),
                "category" => descending ? products.OrderByDescending(p => p.Category.Name) : products.OrderBy(p => p.Category.Name),
                _ => descending ? products.OrderByDescending(p => p.Name) : products.OrderBy(p => p.Name)
            };

            var totalCount = await products.CountAsync();

            var items = await ordered
                .ThenBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new CatalogProductDto
                {
                    Id = p.Id,
                    Name = p.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? p.Name,
                    ImageUrl = p.ImageUrl,
                    DefaultUnit = p.Unit,
                    DefaultShelfLifeDays = p.DefaultShelfLifeDays ?? 7,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? p.Category.Name,
                    CategoryImageUrl = p.Category.ImageUrl,
                    CreatedByUserId = p.CreatedByUserId
                })
                .ToListAsync();

            var customCount = await _dbContext.Products.CountAsync(p => p.CreatedByUserId != null);

            return ServiceResult<AdminCatalogProductsPageDto>.Success(new AdminCatalogProductsPageDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                CustomProductsCount = customCount
            });
        }

        public async Task<ServiceResult<CatalogProductDto>> GetByIdAsync(Guid id, string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLowerInvariant();
            var targetLangId = await _cache.GetLanguageIdAsync(
                _dbContext, $"lang:catalog:{normalizedLang}",
                l => l.Code.ToLower() == normalizedLang || l.Code.ToLower().StartsWith(normalizedLang));

            var product = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(CreateDtoSelector(targetLangId))
                .FirstOrDefaultAsync();

            return product != null
                ? ServiceResult<CatalogProductDto>.Success(product)
                : ServiceResult<CatalogProductDto>.Failure($"Produkt w katalogu o ID '{id}' nie został znaleziony.");
        }

        public async Task<ServiceResult<CatalogProductDto>> CreateAsync(CreateOrUpdateCatalogProductDto createDto, Guid? currentUserId = null)
        {
            if (!await _dbContext.Categories.AnyAsync(c => c.Id == createDto.CategoryId))
                return ServiceResult<CatalogProductDto>.Failure("Wskazana kategoria nie istnieje.");

            var imageUrl = await _fileStorageService.ProcessImageAsync(createDto.ImageUrl, null, "products");

            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = createDto.CategoryId,
                Name = createDto.Name.Trim(),
                ImageUrl = imageUrl,
                Unit = createDto.DefaultUnit ?? "szt.",
                DefaultShelfLifeDays = createDto.DefaultShelfLifeDays,
                CreatedByUserId = currentUserId
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(product.Id);
        }

        public async Task<ServiceResult<CatalogProductDto>> UpdateAsync(Guid id, CreateOrUpdateCatalogProductDto updateDto, Guid? currentUserId = null, bool isAdmin = false)
        {
            var product = await _dbContext.Products
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return ServiceResult<CatalogProductDto>.Failure($"Produkt o ID '{id}' nie został znaleziony.");
            if (!isAdmin && product.CreatedByUserId != currentUserId) return ServiceResult<CatalogProductDto>.Failure("Brak uprawnień do edycji tego produktu w katalogu.");
            if (!await _dbContext.Categories.AnyAsync(c => c.Id == updateDto.CategoryId)) return ServiceResult<CatalogProductDto>.Failure("Wskazana kategoria nie istnieje.");

            if (!string.Equals(product.Name, updateDto.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (product.Translations.Any())
                    _dbContext.ProductTranslations.RemoveRange(product.Translations);
            }

            product.CategoryId = updateDto.CategoryId;
            product.Name = updateDto.Name.Trim();
            product.ImageUrl = await _fileStorageService.ProcessImageAsync(updateDto.ImageUrl, product.ImageUrl, "products");
            product.Unit = updateDto.DefaultUnit ?? "szt.";
            product.DefaultShelfLifeDays = updateDto.DefaultShelfLifeDays;

            await _dbContext.SaveChangesAsync();
            return await GetByIdAsync(product.Id);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid? currentUserId = null, bool isAdmin = false)
        {
            var product = await _dbContext.Products.FindAsync(id);
            if (product == null) return ServiceResult<bool>.Failure($"Produkt o ID '{id}' nie został znaleziony.");
            if (!isAdmin && product.CreatedByUserId != currentUserId) return ServiceResult<bool>.Failure("Brak uprawnień do usunięcia tego produktu z katalogu.");

            var isUsed = await _dbContext.RecipeIngredients.AnyAsync(ri => ri.ProductId == id)
                      || await _dbContext.PantryItems.AnyAsync(pi => pi.ProductId == id)
                      || await _dbContext.ShoppingListItems.AnyAsync(sli => sli.ProductId == id);

            if (isUsed)
            {
                return ServiceResult<bool>.Failure("Nie można usunąć produktu, ponieważ jest powiązany z przepisami, lodówką lub listą zakupów.");
            }

            if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                _fileStorageService.DeleteFile(product.ImageUrl);

            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<IEnumerable<CategoryDto>>> GetCategoriesAsync(string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLowerInvariant();
            var targetLangId = await _cache.GetLanguageIdAsync(
                _dbContext, $"lang:catalog:{normalizedLang}",
                l => l.Code.ToLower() == normalizedLang || l.Code.ToLower().StartsWith(normalizedLang));

            var categories = await _dbContext.Categories
                .AsNoTracking()
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? c.Name,
                    ImageUrl = c.ImageUrl,
                    DisplayOrder = c.DisplayOrder,
                    IsPantryStaple = c.IsPantryStaple
                })
                .ToListAsync();

            return ServiceResult<IEnumerable<CategoryDto>>.Success(categories);
        }

        public async Task<Guid> GetOrCreateCustomProductAsync(
            string name,
            Guid? categoryId = null,
            string? unit = null,
            string? imageUrl = null)
        {
            var userId = _currentUserService.UserId;
            var trimmedName = name.Trim();
            var normalized = trimmedName.ToLower();

            var systemProduct = await _dbContext.Products
                .FirstOrDefaultAsync(p => p.CreatedByUserId == null && p.Name.ToLower() == normalized);

            if (systemProduct != null) return systemProduct.Id;

            var existing = await _dbContext.Products
                .FirstOrDefaultAsync(p => p.CreatedByUserId == userId && p.Name.ToLower() == normalized);

            if (existing != null) return existing.Id;

            var targetCategoryId = categoryId is { } catId && catId != Guid.Empty
                ? catId
                : (await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name == "Inne")
                   ?? await _dbContext.Categories.OrderBy(c => c.DisplayOrder).FirstOrDefaultAsync())?.Id ?? Guid.Empty;

            var newProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                CategoryId = targetCategoryId,
                ImageUrl = imageUrl,
                Unit = !string.IsNullOrWhiteSpace(unit) ? unit : "szt.",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Products.Add(newProduct);
            await _dbContext.SaveChangesAsync();

            return newProduct.Id;
        }
    }
}