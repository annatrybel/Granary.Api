using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Granary.Api.Services.Catalog
{
    public class CatalogProductService : ICatalogProductService
    {
        private readonly GranaryDbContext _dbContext;
        private readonly ILogger<CatalogProductService> _logger;

        public CatalogProductService(GranaryDbContext dbContext, ILogger<CatalogProductService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        private static Expression<Func<Product, CatalogProductDto>> CreateDtoSelector(Guid targetLangId) => p => new CatalogProductDto
        {
            Id = p.Id,
            Name = p.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? p.Name,
            Barcode = p.Barcode,
            ImageUrl = p.ImageUrl,
            DefaultUnit = p.Translations.Where(t => t.LanguageId == targetLangId && t.Unit != null).Select(t => t.Unit).FirstOrDefault() ?? p.DefaultUnit,
            DefaultShelfLifeDays = p.DefaultShelfLifeDays ?? 7,
            CategoryId = p.CategoryId,
            CategoryName = p.Category.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? p.Category.Name,
            CreatedByUserId = p.CreatedByUserId
        };

        public async Task<ServiceResult<IEnumerable<CatalogProductDto>>> SearchAsync(
            string? query,
            Guid? categoryId,
            Guid? currentUserId = null,
            bool excludePantry = false,
            string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLower();

            var targetLang = await _dbContext.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Code.ToLower() == normalizedLang || l.Code.ToLower().StartsWith(normalizedLang));
            var targetLangId = targetLang?.Id ?? Guid.Empty;

            var dbQuery = _dbContext.Products
                .AsNoTracking()
                .Include(p => p.Translations)
                .Include(p => p.Category)
                    .ThenInclude(c => c.Translations)
                .AsQueryable();

            if (currentUserId.HasValue)
            {
                dbQuery = dbQuery.Where(p => p.CreatedByUserId == null || p.CreatedByUserId == currentUserId.Value);

                if (excludePantry)
                {
                    var userActiveProductIds = await _dbContext.PantryItems
                        .AsNoTracking()
                        .Where(pi => pi.UserId == currentUserId.Value && pi.Status == PantryItemStatus.Active)
                        .Select(pi => pi.ProductId)
                        .ToListAsync();

                    dbQuery = dbQuery.Where(p => !userActiveProductIds.Contains(p.Id));
                }
            }
            else
            {
                dbQuery = dbQuery.Where(p => p.CreatedByUserId == null);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalized = query.ToLower().Trim();
                dbQuery = dbQuery.Where(p =>
                    p.Name.ToLower().Contains(normalized) ||
                    (p.Barcode != null && p.Barcode.Contains(normalized)) ||
                    p.Translations.Any(t => t.LanguageId == targetLangId && t.Name.ToLower().Contains(normalized))
                );
            }

            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                dbQuery = dbQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            var results = await dbQuery
                .Take(50)
                .Select(CreateDtoSelector(targetLangId))
                .ToListAsync();

            return ServiceResult<IEnumerable<CatalogProductDto>>.Success(results);
        }

        public async Task<ServiceResult<CatalogProductDto>> GetByIdAsync(Guid id, string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLower();
            var targetLang = await _dbContext.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Code.ToLower() == normalizedLang || l.Code.ToLower().StartsWith(normalizedLang));
            var targetLangId = targetLang?.Id ?? Guid.Empty;

            var product = await _dbContext.Products
                .AsNoTracking()
                .Include(p => p.Translations)
                .Include(p => p.Category)
                    .ThenInclude(c => c.Translations)
                .Where(p => p.Id == id)
                .Select(CreateDtoSelector(targetLangId))
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return ServiceResult<CatalogProductDto>.Failure($"Produkt w katalogu o ID '{id}' nie został znaleziony.");
            }

            return ServiceResult<CatalogProductDto>.Success(product);
        }

        public async Task<ServiceResult<CatalogProductDto>> GetByBarcodeAsync(string barcode, string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLower();
            var targetLang = await _dbContext.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Code.ToLower() == normalizedLang || l.Code.ToLower().StartsWith(normalizedLang));
            var targetLangId = targetLang?.Id ?? Guid.Empty;

            var product = await _dbContext.Products
                .AsNoTracking()
                .Include(p => p.Translations)
                .Include(p => p.Category)
                    .ThenInclude(c => c.Translations)
                .Where(p => p.Barcode == barcode)
                .Select(CreateDtoSelector(targetLangId))
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return ServiceResult<CatalogProductDto>.Failure($"Produkt o kodzie kreskowym '{barcode}' nie został znaleziony.");
            }

            return ServiceResult<CatalogProductDto>.Success(product);
        }

        public async Task<ServiceResult<CatalogProductDto>> CreateAsync(CreateOrUpdateCatalogProductDto createDto, Guid? currentUserId = null)
        {
            var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == createDto.CategoryId);
            if (!categoryExists)
            {
                return ServiceResult<CatalogProductDto>.Failure("Wskazana kategoria nie istnieje.");
            }

            if (!string.IsNullOrWhiteSpace(createDto.Barcode))
            {
                var barcodeExists = await _dbContext.Products.AnyAsync(p => p.Barcode == createDto.Barcode);
                if (barcodeExists)
                {
                    return ServiceResult<CatalogProductDto>.Failure($"Produkt z kodem kreskowym '{createDto.Barcode}' już istnieje w bazie.");
                }
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = createDto.CategoryId,
                Name = createDto.Name,
                Barcode = createDto.Barcode,
                ImageUrl = createDto.ImageUrl,
                DefaultUnit = createDto.DefaultUnit ?? "szt.",
                DefaultShelfLifeDays = createDto.DefaultShelfLifeDays,
                CreatedByUserId = currentUserId
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Dodano nowy produkt katalogowy: {ProductName} (ID: {ProductId})", product.Name, product.Id);

            return await GetByIdAsync(product.Id);
        }

        public async Task<ServiceResult<CatalogProductDto>> UpdateAsync(Guid id, CreateOrUpdateCatalogProductDto updateDto, Guid? currentUserId = null, bool isAdmin = false)
        {
            var product = await _dbContext.Products
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return ServiceResult<CatalogProductDto>.Failure($"Produkt o ID '{id}' nie został znaleziony.");
            }

            if (!isAdmin && product.CreatedByUserId != currentUserId)
            {
                return ServiceResult<CatalogProductDto>.Failure("Brak uprawnień do edycji tego produktu w katalogu.");
            }

            var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == updateDto.CategoryId);
            if (!categoryExists)
            {
                return ServiceResult<CatalogProductDto>.Failure("Wskazana kategoria nie istnieje.");
            }

            // Inwalidacja starych tłumaczeń przy zmianie polskiej nazwy:
            if (!string.Equals(product.Name, updateDto.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (product.Translations.Any())
                {
                    _dbContext.ProductTranslations.RemoveRange(product.Translations);
                }
            }

            product.CategoryId = updateDto.CategoryId;
            product.Name = updateDto.Name;
            product.Barcode = updateDto.Barcode;
            product.ImageUrl = updateDto.ImageUrl;
            product.DefaultUnit = updateDto.DefaultUnit ?? "szt.";
            product.DefaultShelfLifeDays = updateDto.DefaultShelfLifeDays;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Zaktualizowano produkt katalogowy o ID: {ProductId}", product.Id);

            return await GetByIdAsync(product.Id);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid id, Guid? currentUserId = null, bool isAdmin = false)
        {
            var product = await _dbContext.Products.FindAsync(id);

            if (product == null)
            {
                return ServiceResult<bool>.Failure($"Produkt o ID '{id}' nie został znaleziony.");
            }

            if (!isAdmin && product.CreatedByUserId != currentUserId)
            {
                return ServiceResult<bool>.Failure("Brak uprawnień do usunięcia tego produktu z katalogu.");
            }

            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Usunięto produkt z katalogu o ID: {ProductId}", id);

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<IEnumerable<CategoryDto>>> GetCategoriesAsync(string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLower();
            var targetLang = await _dbContext.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Code.ToLower() == normalizedLang || l.Code.ToLower().StartsWith(normalizedLang));
            var targetLangId = targetLang?.Id ?? Guid.Empty;

            var categories = await _dbContext.Categories
                .AsNoTracking()
                .Include(c => c.Translations)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? c.Name,
                    ImageUrl = c.ImageUrl,
                    DisplayOrder = c.DisplayOrder
                })
                .ToListAsync();

            return ServiceResult<IEnumerable<CategoryDto>>.Success(categories);
        }

        public async Task<Guid> GetOrCreateCustomProductAsync(
            Guid userId,
            string name,
            Guid? categoryId = null,
            string? unit = null,
            string? imageUrl = null)
        {
            var trimmedName = name.Trim();
            var existing = await _dbContext.Products
                .FirstOrDefaultAsync(p => p.CreatedByUserId == userId && p.Name.ToLower() == trimmedName.ToLower());

            if (existing != null)
            {
                return existing.Id;
            }

            Guid targetCategoryId;
            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                targetCategoryId = categoryId.Value;
            }
            else
            {
                var defaultCategory = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name == "Inne")
                                   ?? await _dbContext.Categories.OrderBy(c => c.DisplayOrder).FirstOrDefaultAsync();

                targetCategoryId = defaultCategory?.Id ?? Guid.Empty;
            }

            var newProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                CategoryId = targetCategoryId,
                ImageUrl = imageUrl,
                DefaultUnit = !string.IsNullOrWhiteSpace(unit) ? unit : "szt.",
                CreatedByUserId = userId
            };

            _dbContext.Products.Add(newProduct);
            await _dbContext.SaveChangesAsync();

            return newProduct.Id;
        }
    }
}