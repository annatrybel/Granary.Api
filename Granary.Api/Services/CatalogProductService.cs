using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
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

        private static readonly Expression<Func<Product, CatalogProductDto>> DtoSelector = p => new CatalogProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Barcode = p.Barcode,
            Emoji = p.Emoji,
            DefaultUnit = p.DefaultUnit,
            DefaultShelfLifeDays = p.DefaultShelfLifeDays ?? 7,
            CategoryId = p.CategoryId,
            CategoryName = p.Category.Name,
            CreatedByUserId = p.CreatedByUserId
        };

        public async Task<ServiceResult<IEnumerable<CatalogProductDto>>> SearchAsync(string? query, Guid? categoryId, Guid? currentUserId = null)
        {
            var dbQuery = _dbContext.Products.AsNoTracking();

            if (currentUserId.HasValue)
            {
                dbQuery = dbQuery.Where(p => p.CreatedByUserId == null || p.CreatedByUserId == currentUserId.Value);
            }
            else
            {
                dbQuery = dbQuery.Where(p => p.CreatedByUserId == null);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalized = query.ToLower().Trim();
                dbQuery = dbQuery.Where(p => p.Name.ToLower().Contains(normalized) || (p.Barcode != null && p.Barcode.Contains(normalized)));
            }

            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                dbQuery = dbQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            var results = await dbQuery
                .Take(50)
                .Select(DtoSelector)
                .ToListAsync();

            return ServiceResult<IEnumerable<CatalogProductDto>>.Success(results);
        }

        public async Task<ServiceResult<CatalogProductDto>> GetByIdAsync(Guid id)
        {
            var product = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(DtoSelector)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return ServiceResult<CatalogProductDto>.Failure($"Produkt w katalogu o ID '{id}' nie został znaleziony.");
            }

            return ServiceResult<CatalogProductDto>.Success(product);
        }

        public async Task<ServiceResult<CatalogProductDto>> GetByBarcodeAsync(string barcode)
        {
            var product = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.Barcode == barcode)
                .Select(DtoSelector)
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
                Emoji = createDto.Emoji,
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
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);

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

            product.CategoryId = updateDto.CategoryId;
            product.Name = updateDto.Name;
            product.Barcode = updateDto.Barcode;
            product.Emoji = updateDto.Emoji;
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
    }
}