using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Granary.Api.Services
{
    public class ProductService : IProductService
    {
        private readonly GranaryDbContext _dbContext;
        private readonly ILogger<ProductService> _logger;

        public ProductService(GranaryDbContext dbContext, ILogger<ProductService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        private static readonly Expression<Func<Product, ProductDto>> ProductDtoSelector = product => new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            Emoji = product.Emoji,
            DefaultUnit = product.DefaultUnit,
            DefaultShelfLifeDays = product.DefaultShelfLifeDays,
            CategoryId = product.CategoryId,
            CategoryName = product.Category.Name
        };

        public async Task<ServiceResult<IEnumerable<ProductDto>>> GetAllAsync(Guid? currentUserId = null)
        {
            var query = _dbContext.Products.AsNoTracking();

            if (currentUserId.HasValue)
            {
                query = query.Where(p => p.CreatedByUserId == null || p.CreatedByUserId == currentUserId.Value);
            }
            else
            {
                query = query.Where(p => p.CreatedByUserId == null);
            }

            var products = await query
                .Select(ProductDtoSelector)
                .ToListAsync();

            return ServiceResult<IEnumerable<ProductDto>>.Success(products);
        }

        public async Task<ServiceResult<ProductDto>> GetByIdAsync(Guid id)
        {
            var product = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(ProductDtoSelector) 
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return ServiceResult<ProductDto>.Failure($"Produkt o ID '{id}' nie został znaleziony.");
            }

            return ServiceResult<ProductDto>.Success(product);
        }

        public async Task<ServiceResult<ProductDto>> GetByBarcodeAsync(string barcode)
        {
            var product = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.Barcode == barcode)
                .Select(ProductDtoSelector) 
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return ServiceResult<ProductDto>.Failure($"Produkt o kodzie kreskowym '{barcode}' nie został znaleziony.");
            }

            return ServiceResult<ProductDto>.Success(product);
        }

        public async Task<ServiceResult<ProductDto>> CreateAsync(CreateOrUpdateProductDto createDto, Guid? currentUserId = null)
        {
            var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == createDto.CategoryId);
            if (!categoryExists)
            {
                return ServiceResult<ProductDto>.Failure("Wskazana kategoria nie istnieje.");
            }

            if (!string.IsNullOrWhiteSpace(createDto.Barcode))
            {
                var barcodeExists = await _dbContext.Products.AnyAsync(p => p.Barcode == createDto.Barcode);
                if (barcodeExists)
                {
                    return ServiceResult<ProductDto>.Failure($"Produkt z kodem kreskowym '{createDto.Barcode}' już istnieje w systemie.");
                }
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = createDto.CategoryId,
                Name = createDto.Name,
                Barcode = createDto.Barcode,
                Emoji = createDto.Emoji,
                DefaultUnit = createDto.DefaultUnit,
                DefaultShelfLifeDays = createDto.DefaultShelfLifeDays,
                CreatedByUserId = currentUserId
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Dodano nowy produkt spożywczy: {ProductName} (ID: {ProductId})", product.Name, product.Id);

            
            return await GetByIdAsync(product.Id);
        }

        public async Task<ServiceResult<ProductDto>> UpdateAsync(Guid id, CreateOrUpdateProductDto updateDto)
        {
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return ServiceResult<ProductDto>.Failure($"Produkt o ID '{id}' nie został znaleziony.");
            }

            var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == updateDto.CategoryId);
            if (!categoryExists)
            {
                return ServiceResult<ProductDto>.Failure("Wskazana kategoria nie istnieje.");
            }

            product.CategoryId = updateDto.CategoryId;
            product.Name = updateDto.Name;
            product.Barcode = updateDto.Barcode;
            product.Emoji = updateDto.Emoji;
            product.DefaultUnit = updateDto.DefaultUnit;
            product.DefaultShelfLifeDays = updateDto.DefaultShelfLifeDays;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Zaktualizowano produkt spożywczy o ID: {ProductId}", product.Id);

            return await GetByIdAsync(product.Id);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
        {
            var product = await _dbContext.Products.FindAsync(id);

            if (product == null)
            {
                return ServiceResult<bool>.Failure($"Produkt o ID '{id}' nie został znaleziony.");
            }

            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Usunięto produkt o ID: {ProductId}", id);

            return ServiceResult<bool>.Success(true);
        }
    }
}