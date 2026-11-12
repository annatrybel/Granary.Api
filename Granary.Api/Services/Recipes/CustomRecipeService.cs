using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Auth;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Granary.Api.Services.Recipes
{
    public class CustomRecipeService : ICustomRecipeService
    {
        private readonly GranaryDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IRecipeService _recipeService;

        public CustomRecipeService(
            GranaryDbContext context,
            IFileStorageService fileStorageService,
            ICurrentUserService currentUserService,
            IRecipeService recipeService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _currentUserService = currentUserService;
            _recipeService = recipeService;
        }

        public async Task<ServiceResult<RecipeDetailDto>> CreateCustomRecipeAsync(CreateCustomRecipeDto dto)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrWhiteSpace(dto.Title))
                return ServiceResult<RecipeDetailDto>.Failure("Nazwa przepisu nie może być pusta.");

            var persistentImageUrl = await _fileStorageService.ProcessImageAsync(dto.ImageUrl, null, "recipes");

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                ImageUrl = persistentImageUrl,
                Instructions = BuildInstructions(dto.Steps),
                PrepTimeMinutes = dto.PrepTimeMinutes > 0 ? dto.PrepTimeMinutes : 15,
                Servings = dto.Servings > 0 ? dto.Servings : 1,
                Calories = dto.Calories,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            recipe.Ingredients = await BuildIngredientsAsync(recipe.Id, dto, userId);

            if (dto.DietIds != null && dto.DietIds.Any())
            {
                recipe.RecipeDiets = dto.DietIds.Distinct().Select(dId => new RecipeDiet
                {
                    RecipeId = recipe.Id,
                    DietId = dId
                }).ToList();
            }

            _context.Recipes.Add(recipe);
            _context.UserRecipeInteractions.Add(new UserRecipeInteraction
            {
                UserId = userId,
                RecipeId = recipe.Id,
                IsFavorite = true,
                FavoritedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return await _recipeService.GetByIdAsync(recipe.Id);
        }

        public async Task<ServiceResult<RecipeDetailDto>> UpdateCustomRecipeAsync(Guid recipeId, CreateCustomRecipeDto dto)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrWhiteSpace(dto.Title))
                return ServiceResult<RecipeDetailDto>.Failure("Nazwa przepisu nie może być pusta.");

            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.RecipeDiets)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
                return ServiceResult<RecipeDetailDto>.Failure("Przepis nie istnieje.");

            if (recipe.CreatedByUserId != userId)
                return ServiceResult<RecipeDetailDto>.Failure("Brak uprawnień do edycji tego przepisu.");

            var oldCustomProductIds = await _context.Products
                .Where(p => recipe.Ingredients.Select(i => i.ProductId).Contains(p.Id) && p.CreatedByUserId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            recipe.ImageUrl = await _fileStorageService.ProcessImageAsync(dto.ImageUrl, recipe.ImageUrl, "recipes");
            recipe.Title = dto.Title.Trim();
            recipe.Instructions = BuildInstructions(dto.Steps, recipe.Instructions);
            recipe.PrepTimeMinutes = dto.PrepTimeMinutes > 0 ? dto.PrepTimeMinutes : recipe.PrepTimeMinutes;
            recipe.Servings = dto.Servings > 0 ? dto.Servings : recipe.Servings;
            recipe.Calories = dto.Calories;

            _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
            recipe.Ingredients = await BuildIngredientsAsync(recipe.Id, dto, userId);

            _context.RecipeDiets.RemoveRange(recipe.RecipeDiets);
            if (dto.DietIds != null && dto.DietIds.Any())
            {
                var newDiets = dto.DietIds.Distinct().Select(dId => new RecipeDiet
                {
                    RecipeId = recipe.Id,
                    DietId = dId
                });
                _context.RecipeDiets.AddRange(newDiets);
            }

            await _context.SaveChangesAsync();

            foreach (var oldProductId in oldCustomProductIds)
            {
                await TryCleanupOrphanedProductAsync(oldProductId, userId);
            }

            return await _recipeService.GetByIdAsync(recipe.Id);
        }

        public async Task<ServiceResult<bool>> DeleteRecipeAsync(Guid recipeId)
        {
            var userId = _currentUserService.UserId;
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.RecipeDiets)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
                return ServiceResult<bool>.Failure("Przepis nie istnieje.");

            if (recipe.CreatedByUserId != userId)
                return ServiceResult<bool>.Failure("Brak uprawnień do usunięcia tego przepisu.");

            var ingredientProductIds = recipe.Ingredients.Select(i => i.ProductId).Distinct().ToList();
            var customProductIds = await _context.Products
                .Where(p => ingredientProductIds.Contains(p.Id) && p.CreatedByUserId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(recipe.ImageUrl))
                _fileStorageService.DeleteFile(recipe.ImageUrl);

            var interactions = await _context.UserRecipeInteractions.Where(x => x.RecipeId == recipeId).ToListAsync();
            _context.UserRecipeInteractions.RemoveRange(interactions);
            _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
            _context.RecipeDiets.RemoveRange(recipe.RecipeDiets);
            _context.Recipes.Remove(recipe);

            await _context.SaveChangesAsync();

            foreach (var productId in customProductIds)
            {
                await TryCleanupOrphanedProductAsync(productId, userId);
            }

            return ServiceResult<bool>.Success(true);
        }

        #region Helpers

        private async Task TryCleanupOrphanedProductAsync(Guid productId, Guid userId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.CreatedByUserId == userId);

            if (product == null) return;

            var isUsedInRecipes = await _context.RecipeIngredients.AnyAsync(ri => ri.ProductId == productId);
            var isInPantry = await _context.PantryItems.AnyAsync(pi => pi.ProductId == productId && pi.UserId == userId);
            var isInShoppingList = await _context.ShoppingListItems.AnyAsync(sli => sli.ProductId == productId);

            if (!isUsedInRecipes && !isInPantry && !isInShoppingList)
            {
                if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                    _fileStorageService.DeleteFile(product.ImageUrl);

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<List<RecipeIngredient>> BuildIngredientsAsync(Guid recipeId, CreateCustomRecipeDto dto, Guid userId)
        {
            if (dto.Ingredients == null || !dto.Ingredients.Any())
                return new List<RecipeIngredient>();

            var defaultCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == "inne")
                             ?? await _context.Categories.FirstOrDefaultAsync();
            var defaultCategoryId = defaultCat?.Id ?? Guid.NewGuid();

            var ingredients = new List<RecipeIngredient>();

            foreach (var ingDto in dto.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i.Name)))
            {
                var (parsedQty, parsedUnit) = ParseQuantityAndUnit(ingDto.Quantity);
                Product? product = null;

                if (ingDto.ProductId.HasValue && ingDto.ProductId.Value != Guid.Empty)
                    product = await _context.Products.FirstOrDefaultAsync(p => p.Id == ingDto.ProductId.Value);

                if (product == null)
                {
                    var normalizedName = ingDto.Name.Trim().ToLower();

                    product = await _context.Products
                        .Where(p => p.Name.ToLower() == normalizedName &&
                                    (p.CreatedByUserId == null || p.CreatedByUserId == userId))
                        .OrderBy(p => p.CreatedByUserId != null)
                        .FirstOrDefaultAsync();

                    if (product == null)
                    {
                        product = new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = ingDto.Name.Trim(),
                            CategoryId = defaultCategoryId,
                            Unit = parsedUnit,
                            CreatedByUserId = userId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Products.Add(product);
                        await _context.SaveChangesAsync();
                    }
                }

                ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipeId,
                    ProductId = product.Id,
                    Quantity = parsedQty,
                    Unit = parsedUnit,
                    IsOptional = false
                });
            }

            return ingredients;
        }

        private static string BuildInstructions(IEnumerable<string>? steps, string fallback = "Brak szczegółowej instrukcji.")
        {
            var valid = steps?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            return valid != null && valid.Any() ? string.Join("\n", valid) : fallback;
        }

        private static (decimal quantity, string unit) ParseQuantityAndUnit(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return (1m, "szt.");
            var match = Regex.Match(input.Trim().Replace(',', '.'), @"^([0-9]+(?:\.[0-9]+)?)\s*(.*)$");

            return match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty)
                ? (qty, string.IsNullOrWhiteSpace(match.Groups[2].Value) ? "szt." : match.Groups[2].Value.Trim())
                : (1m, "szt.");
        }

        #endregion
    }
}