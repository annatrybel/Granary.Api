using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<RecipeService> _logger;

        public RecipeService(GranaryDbContext context, ILogger<RecipeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<IEnumerable<RecipeMatchDto>>> GetSuggestedRecipesAsync(Guid userId, IEnumerable<Guid>? selectedProductIds = null)
        {
            var userPantry = await _context.PantryItems
                .AsNoTracking()
                .Where(pi => pi.UserId == userId && pi.Status == PantryItemStatus.Active)
                .Select(pi => new
                {
                    pi.ProductId,
                    IsExpiringSoon = pi.ExpirationDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))
                })
                .ToListAsync();

            HashSet<Guid> targetProductIds;
            var expiringProductIds = userPantry.Where(p => p.IsExpiringSoon).Select(p => p.ProductId).ToHashSet();

            if (selectedProductIds != null && selectedProductIds.Any())
            {
                targetProductIds = selectedProductIds.ToHashSet();
            }
            else
            {
                targetProductIds = userPantry.Select(p => p.ProductId).ToHashSet();
            }

            var recipes = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Product)
                .ToListAsync();

            var matched = recipes.Select(recipe =>
            {
                var requiredIngredients = recipe.Ingredients.Where(i => !i.IsOptional).ToList();
                var totalCount = requiredIngredients.Count;

                var owned = requiredIngredients
                    .Where(i => targetProductIds.Contains(i.ProductId))
                    .ToList();

                var missing = requiredIngredients
                    .Where(i => !targetProductIds.Contains(i.ProductId))
                    .Select(i => new MissingIngredientDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product?.Name ?? "Składnik",
                        Quantity = i.Quantity,
                        Unit = i.Unit
                    })
                    .ToList();

                var ownedCount = owned.Count;
                var matchPct = totalCount > 0 ? (int)Math.Round((double)ownedCount / totalCount * 100) : 0;
                var expiringSaved = owned.Count(i => expiringProductIds.Contains(i.ProductId));

                var summary = string.Join(", ", owned.Take(3).Select(i => i.Product?.Name).Where(n => !string.IsNullOrEmpty(n)));

                return new RecipeMatchDto
                {
                    RecipeId = recipe.Id,
                    Title = recipe.Title,
                    Description = recipe.Description,
                    ImageUrl = recipe.ImageUrl,
                    PrepTimeMinutes = recipe.PrepTimeMinutes,
                    Servings = recipe.Servings,
                    MatchPercentage = matchPct,
                    OwnedIngredientsCount = ownedCount,
                    TotalIngredientsCount = totalCount,
                    ExpiringIngredientsSaved = expiringSaved,
                    CanBeCookedNow = missing.Count == 0,
                    UsedIngredientsSummary = string.IsNullOrEmpty(summary) ? "Brak składników ze spiżarni" : $"Wykorzystasz: {summary}",
                    MissingIngredients = missing
                };
            })

            .Where(r => r.OwnedIngredientsCount > 0 || !targetProductIds.Any())
            .OrderByDescending(r => r.CanBeCookedNow)
            .ThenByDescending(r => r.ExpiringIngredientsSaved)
            .ThenByDescending(r => r.MatchPercentage)
            .ToList();

            return ServiceResult<IEnumerable<RecipeMatchDto>>.Success(matched);
        }

        public async Task<ServiceResult<RecipeDetailDto>> GetByIdAsync(Guid recipeId, Guid userId)
        {
            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Product)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
            {
                return ServiceResult<RecipeDetailDto>.Failure("Nie znaleziono wybranego przepisu.");
            }

            var ownedProductIds = await _context.PantryItems
                .AsNoTracking()
                .Where(pi => pi.UserId == userId && pi.Status == PantryItemStatus.Active)
                .Select(pi => pi.ProductId)
                .ToHashSetAsync();

            var detail = new RecipeDetailDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Description = recipe.Description,
                Instructions = recipe.Instructions,
                ImageUrl = recipe.ImageUrl,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                Servings = recipe.Servings,
                Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "Składnik",
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    IsOptional = i.IsOptional,
                    IsOwnedInPantry = ownedProductIds.Contains(i.ProductId)
                }).ToList()
            };

            return ServiceResult<RecipeDetailDto>.Success(detail);
        }
    }
}
