using System.Text.Json;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Seeders
{
    public class ProductSeeder
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<ProductSeeder> _logger;

        public ProductSeeder(GranaryDbContext context, ILogger<ProductSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            var categories = await _context.Categories.ToListAsync();
            if (!categories.Any())
            {
                _logger.LogWarning("Brak kategorii w bazie. Najpierw uruchom CategorySeeder!");
                return;
            }

            var categoryMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var cat in categories)
            {
                if (!string.IsNullOrEmpty(cat.IconSlug))
                    categoryMap[cat.IconSlug] = cat.Id;

                categoryMap[cat.Name] = cat.Id;
            }

            var defaultCategoryId = categories.First().Id;

            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Seeders", "Data", "products.json");
            if (!File.Exists(jsonPath))
            {
                jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Seeders", "Data", "products.json");
            }

            if (!File.Exists(jsonPath))
            {
                _logger.LogWarning("Nie znaleziono pliku products.json pod ścieżką: {Path}", jsonPath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var productSeedList = JsonSerializer.Deserialize<List<ProductJsonModel>>(jsonContent, options);

            if (productSeedList == null || !productSeedList.Any())
            {
                return;
            }

            var existingProducts = await _context.Products.ToListAsync();
            var existingMap = existingProducts.ToDictionary(p => p.Name.ToLower().Trim(), p => p);

            var toAdd = new List<Product>();
            int updatedCount = 0;

            foreach (var item in productSeedList)
            {
                var normalizedName = item.Name.ToLower().Trim();

                var catId = defaultCategoryId;
                if (!string.IsNullOrEmpty(item.CategorySlug) && categoryMap.TryGetValue(item.CategorySlug, out var foundCatId))
                {
                    catId = foundCatId;
                }

                if (existingMap.TryGetValue(normalizedName, out var existingProduct))
                {
                    bool wasModified = false;

                    if (string.IsNullOrEmpty(existingProduct.ImageUrl) && !string.IsNullOrEmpty(item.ImageUrl))
                    {
                        existingProduct.ImageUrl = item.ImageUrl;
                        wasModified = true;
                    }

                    if (string.IsNullOrEmpty(existingProduct.Barcode) && !string.IsNullOrEmpty(item.Barcode))
                    {
                        existingProduct.Barcode = item.Barcode;
                        wasModified = true;
                    }

                    if (wasModified) updatedCount++;
                }
                else
                {
                    toAdd.Add(new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = item.Name.Trim(),
                        CategoryId = catId,
                        ImageUrl = item.ImageUrl,
                        DefaultUnit = item.DefaultUnit ?? "szt.",
                        DefaultShelfLifeDays = item.DefaultShelfLifeDays > 0 ? item.DefaultShelfLifeDays : 7,
                        Barcode = item.Barcode
                    });
                }
            }

            if (toAdd.Any())
            {
                await _context.Products.AddRangeAsync(toAdd);
            }

            if (toAdd.Any() || updatedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("ProductSeeder: Dodano {Added} nowych produktów, zaktualizowano {Updated} istniejących.",
                    toAdd.Count, updatedCount);
            }
        }

        private class ProductJsonModel
        {
            public string Name { get; set; } = string.Empty;
            public string? CategorySlug { get; set; }
            public string? ImageUrl { get; set; }
            public string? DefaultUnit { get; set; }
            public int DefaultShelfLifeDays { get; set; }
            public string? Barcode { get; set; }
        }
    }
}
