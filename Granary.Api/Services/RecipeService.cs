using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Models.Dto;
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

            var userInteractions = await _context.UserRecipeInteractions
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToDictionaryAsync(x => x.RecipeId);

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

                userInteractions.TryGetValue(recipe.Id, out var interaction);

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
                    IsFavorite = interaction?.IsFavorite ?? false, 
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

            var userInteraction = await _context.UserRecipeInteractions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.RecipeId == recipeId);

            var allVotes = await _context.UserRecipeInteractions
                .AsNoTracking()
                .Where(x => x.RecipeId == recipeId && x.Vote != null)
                .Select(x => x.Vote)
                .ToListAsync();

            var detail = new RecipeDetailDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Description = recipe.Description,
                Instructions = recipe.Instructions,
                ImageUrl = recipe.ImageUrl,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                Servings = recipe.Servings,
                IsFavorite = userInteraction?.IsFavorite ?? false,
                LikesCount = allVotes.Count(v => v == true),
                DislikesCount = allVotes.Count(v => v == false),
                UserVote = userInteraction?.Vote,
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

        public async Task<ServiceResult<bool>> ToggleFavoriteAsync(Guid recipeId, Guid userId)
        {
            var interaction = await _context.UserRecipeInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.RecipeId == recipeId);

            if (interaction == null)
            {
                interaction = new UserRecipeInteraction
                {
                    UserId = userId,
                    RecipeId = recipeId,
                    IsFavorite = true,
                    FavoritedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserRecipeInteractions.Add(interaction);
            }
            else
            {
                interaction.IsFavorite = !interaction.IsFavorite;
                interaction.FavoritedAt = interaction.IsFavorite ? DateTime.UtcNow : null;
                interaction.UpdatedAt = DateTime.UtcNow;

                if (!interaction.IsFavorite && interaction.Vote == null)
                {
                    _context.UserRecipeInteractions.Remove(interaction);
                }
            }

            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Success(interaction.IsFavorite);
        }

        public async Task<ServiceResult<VoteResultDto>> VoteRecipeAsync(Guid recipeId, Guid userId, bool isLike)
        {
            var interaction = await _context.UserRecipeInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.RecipeId == recipeId);

            if (interaction == null)
            {
                interaction = new UserRecipeInteraction
                {
                    UserId = userId,
                    RecipeId = recipeId,
                    Vote = isLike,
                    VotedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserRecipeInteractions.Add(interaction);
            }
            else
            {
                if (interaction.Vote == isLike)
                {
                    interaction.Vote = null;
                    interaction.VotedAt = null;
                }
                else
                {
                    interaction.Vote = isLike;
                    interaction.VotedAt = DateTime.UtcNow;
                }

                interaction.UpdatedAt = DateTime.UtcNow;

                if (!interaction.IsFavorite && interaction.Vote == null)
                {
                    _context.UserRecipeInteractions.Remove(interaction);
                }
            }

            await _context.SaveChangesAsync();

            var allVotes = await _context.UserRecipeInteractions
                .AsNoTracking()
                .Where(x => x.RecipeId == recipeId && x.Vote != null)
                .Select(x => x.Vote)
                .ToListAsync();

            return ServiceResult<VoteResultDto>.Success(new VoteResultDto
            {
                LikesCount = allVotes.Count(v => v == true),
                DislikesCount = allVotes.Count(v => v == false),
                UserVote = interaction.Vote
            });
        }

        public async Task<ServiceResult<IEnumerable<RecipeMatchDto>>> GetFavoritesAsync(Guid userId)
        {
            var favRecipeIds = await _context.UserRecipeInteractions
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsFavorite)
                .Select(x => x.RecipeId)
                .ToListAsync();

            var recipes = await _context.Recipes
                .AsNoTracking()
                .Where(r => r.CreatedByUserId == userId || favRecipeIds.Contains(r.Id))
                .Select(r => new RecipeMatchDto
                {
                    RecipeId = r.Id,
                    Title = r.Title,
                    Description = r.Description,
                    ImageUrl = r.ImageUrl,
                    PrepTimeMinutes = r.PrepTimeMinutes,
                    Servings = r.Servings,
                    IsFavorite = true
                })
                .ToListAsync();

            return ServiceResult<IEnumerable<RecipeMatchDto>>.Success(recipes);
        }
    }
}
