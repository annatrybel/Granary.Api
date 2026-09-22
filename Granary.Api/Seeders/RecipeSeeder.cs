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
            if (await _context.Recipes.AnyAsync())
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

            if (seedModels == null || !seedModels.Any())
            {
                return;
            }

            var productsMap = await _context.Products
                .ToDictionaryAsync(p => p.Name.ToLower().Trim(), p => p.Id);

            var recipesToAdd = new List<Recipe>();

            foreach (var rModel in seedModels)
            {
                var recipe = new Recipe
                {
                    Id = Guid.NewGuid(),
                    Title = rModel.Title,
                    Description = rModel.Description,
                    Instructions = rModel.Instructions,
                    PrepTimeMinutes = rModel.PrepTimeMinutes,
                    Servings = rModel.Servings > 0 ? rModel.Servings : 1,
                    ImageUrl = rModel.ImageUrl,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var ingModel in rModel.Ingredients)
                {
                    var normalizedName = ingModel.ProductName.ToLower().Trim();

                    if (productsMap.TryGetValue(normalizedName, out var productId))
                    {
                        recipe.Ingredients.Add(new RecipeIngredient
                        {
                            Id = Guid.NewGuid(),
                            RecipeId = recipe.Id,
                            ProductId = productId,
                            Quantity = ingModel.Quantity,
                            Unit = ingModel.Unit,
                            IsOptional = ingModel.IsOptional
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Składnik '{ProductName}' z przepisu '{RecipeTitle}' nie istnieje w tabeli Products!",
                            ingModel.ProductName, rModel.Title);
                    }
                }

                if (recipe.Ingredients.Any())
                {
                    recipesToAdd.Add(recipe);
                }
            }

            if (recipesToAdd.Any())
            {
                await _context.Recipes.AddRangeAsync(recipesToAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Pomyślnie zaseedowano {Count} przepisów z pliku JSON!", recipesToAdd.Count);
            }
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


