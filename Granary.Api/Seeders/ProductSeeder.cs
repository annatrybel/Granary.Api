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

            var slugToCategoryName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["dairy"] = "Nabiał i Jaja",
                ["bakery"] = "Pieczywo",
                ["fruits"] = "Owoce i Warzywa",
                ["vegetables"] = "Owoce i Warzywa",
                ["meat"] = "Mięso i Wędliny",
                ["fish"] = "Ryby i Owoce morza",
                ["pantry"] = "Spiżarnia i Suche",
                ["sauces"] = "Sosy i Dodatki",
                ["spices"] = "Przyprawy i Zioła",
                ["beverages"] = "Napoje",
                ["snacks"] = "Przekąski",
                ["frozen"] = "Mrożonki"
            };

            var catByName = categories.ToDictionary(c => c.Name.Trim().ToLower(), c => c.Id);
            var defaultCategoryId = categories.FirstOrDefault(c => c.Name.ToLower() == "inne")?.Id ?? categories.First().Id;

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

            if (productSeedList == null || !productSeedList.Any()) return;

            var existingProducts = await _context.Products.ToListAsync();
            var existingMap = existingProducts.ToDictionary(p => p.Name.ToLower().Trim(), p => p);

            var toAdd = new List<Product>();

            foreach (var item in productSeedList)
            {
                var normalizedName = item.Name.ToLower().Trim();

                var catId = defaultCategoryId;
                if (!string.IsNullOrEmpty(item.CategorySlug) &&
                    slugToCategoryName.TryGetValue(item.CategorySlug, out var targetCatName) &&
                    catByName.TryGetValue(targetCatName.ToLower().Trim(), out var foundId))
                {
                    catId = foundId;
                }

                if (!existingMap.ContainsKey(normalizedName))
                {
                    toAdd.Add(new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = item.Name.Trim(),
                        CategoryId = catId,
                        ImageUrl = item.ImageUrl,
                        Unit = item.DefaultUnit ?? "szt.", 
                        DefaultShelfLifeDays = item.DefaultShelfLifeDays > 0 ? item.DefaultShelfLifeDays : 7
                    });
                }
            }

            if (toAdd.Any())
            {
                await _context.Products.AddRangeAsync(toAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation("ProductSeeder: Pomyślnie dodano {Added} produktów.", toAdd.Count);
            }
        }

        private class ProductJsonModel
        {
            public string Name { get; set; } = string.Empty;
            public string? CategorySlug { get; set; }
            public string? ImageUrl { get; set; }
            public string? DefaultUnit { get; set; }
            public int DefaultShelfLifeDays { get; set; }
        }
    }
}
