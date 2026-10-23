using System.Text.Json;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Seeders
{
    public class RecipeSeeder
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<RecipeSeeder> _logger;

        public RecipeSeeder(GranaryDbContext context, ILogger<RecipeSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            if (await _context.Recipes.AnyAsync(r => r.CreatedByUserId == null))
            {
                return;
            }

            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Seeders", "Data", "recipes.json");

            if (!File.Exists(jsonPath))
            {
                jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Seeders", "Data", "recipes.json");
            }

            if (!File.Exists(jsonPath))
            {
                _logger.LogWarning("Nie znaleziono pliku recipes.json pod ścieżką: {Path}", jsonPath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var seedModels = JsonSerializer.Deserialize<List<RecipeJsonModel>>(jsonContent, options);

            if (seedModels == null || !seedModels.Any()) return;

            var existingProducts = await _context.Products.ToListAsync();
            var defaultCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == "inne")
                                  ?? await _context.Categories.FirstOrDefaultAsync();

            var defaultCategoryId = defaultCategory?.Id ?? Guid.NewGuid();

            var existingRecipes = await _context.Recipes
                .AsNoTracking()
                .ToListAsync();

            var existingRecipeMap = existingRecipes
                .ToDictionary(r => r.Title.Trim().ToLower(), r => r.Id);

            var recipesWithIngredients = await _context.RecipeIngredients
                .AsNoTracking()
                .Select(ri => ri.RecipeId)
                .Distinct()
                .ToHashSetAsync();

            foreach (var rModel in seedModels)
            {
                var normalizedTitle = rModel.Title.Trim().ToLower();
                Guid recipeId;

                if (!existingRecipeMap.TryGetValue(normalizedTitle, out recipeId))
                {
                    recipeId = Guid.NewGuid();
                    var newRecipe = new Recipe
                    {
                        Id = recipeId,
                        Title = rModel.Title.Trim(),
                        Instructions = rModel.Instructions,
                        PrepTimeMinutes = rModel.PrepTimeMinutes > 0 ? rModel.PrepTimeMinutes : 15,
                        Servings = rModel.Servings > 0 ? rModel.Servings : 2,
                        ImageUrl = rModel.ImageUrl,
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = null 
                    };

                    _context.Recipes.Add(newRecipe);
                    existingRecipeMap[normalizedTitle] = recipeId;
                }

                if (recipesWithIngredients.Contains(recipeId))
                {
                    continue;
                }

                foreach (var ingModel in rModel.Ingredients)
                {
                    var normalizedIngName = ingModel.ProductName.Trim().ToLower();
                    var product = existingProducts.FirstOrDefault(p => p.Name.Trim().ToLower() == normalizedIngName);

                    if (product == null)
                    {
                        product = existingProducts.FirstOrDefault(p =>
                            p.Name.ToLower().Contains(normalizedIngName) ||
                            normalizedIngName.Contains(p.Name.ToLower()));
                    }

                    if (product == null)
                    {
                        product = new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = ingModel.ProductName.Trim(),
                            CategoryId = defaultCategoryId,
                            DefaultUnit = !string.IsNullOrWhiteSpace(ingModel.Unit) ? ingModel.Unit : "szt.",
                            CreatedByUserId = null,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Products.Add(product);
                        existingProducts.Add(product);
                    }

                    _context.RecipeIngredients.Add(new RecipeIngredient
                    {
                        Id = Guid.NewGuid(),
                        RecipeId = recipeId,
                        ProductId = product.Id,
                        Quantity = ingModel.Quantity > 0 ? ingModel.Quantity : 1,
                        Unit = !string.IsNullOrWhiteSpace(ingModel.Unit) ? ingModel.Unit : "szt.",
                        IsOptional = ingModel.IsOptional
                    });
                }

                recipesWithIngredients.Add(recipeId);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Pomyślnie załadowano przepisy globalne z pliku recipes.json.");
        }

        private class RecipeJsonModel
        {
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string Instructions { get; set; } = string.Empty;
            public int PrepTimeMinutes { get; set; }
            public int Servings { get; set; }
            public string? ImageUrl { get; set; }
            public List<IngredientJsonModel> Ingredients { get; set; } = new();
        }

        private class IngredientJsonModel
        {
            public string ProductName { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public string Unit { get; set; } = "szt.";
            public bool IsOptional { get; set; }
        }
    }
}
