using Granary.Api.Extensions;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Granary.Api.Models.Dto.Recipes;  //do usuniecia
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Auth;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Granary.Api.Services.Recipes
{
    public class RecipeService : IRecipeService
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<RecipeService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMemoryCache _cache;

        public RecipeService(
            GranaryDbContext context,
            ILogger<RecipeService> logger,
            ICurrentUserService currentUserService,
            IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _currentUserService = currentUserService;
            _cache = cache;
        }

        public async Task<ServiceResult<PagedResult<RecipeMatchDto>>> GetFilteredRecipesAsync(RecipeFilterQueryDto filter)
        {
            var userId = _currentUserService.UserId;
            var page = Math.Max(1, filter.Page);
            var pageSize = filter.PageSize < 1 ? 20 : Math.Clamp(filter.PageSize, 1, 100);

            var lang = string.IsNullOrWhiteSpace(filter.Lang) ? "pl" : filter.Lang.Trim().ToLowerInvariant();
            var targetLangId = await _cache.GetLanguageIdAsync(_context, $"lang:recipes:{lang}", l => l.Code.ToLower() == lang);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expiringThreshold = today.AddDays(3);

            var userPantry = await _context.PantryItems
                .AsNoTracking()
                .Where(pi => pi.UserId == userId && pi.Status == PantryItemStatus.Active)
                .Select(pi => new
                {
                    pi.ProductId,
                    IsExpiringSoon = pi.ExpirationDate <= expiringThreshold
                })
                .ToListAsync();

            var userProductIds = userPantry.Select(p => p.ProductId).ToHashSet();
            var expiringProductIds = userPantry.Where(p => p.IsExpiringSoon).Select(p => p.ProductId).ToHashSet();

            var targetProductIds = !string.IsNullOrWhiteSpace(filter.Items)
                ? filter.Items.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToHashSet()
                : new HashSet<Guid>();
            var hasSelectedItemsFilter = targetProductIds.Count > 0;

            var userInteractions = await _context.UserRecipeInteractions
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToDictionaryAsync(x => x.RecipeId);

            var favRecipeIds = userInteractions
                .Where(x => x.Value.IsFavorite)
                .Select(x => x.Key)
                .ToHashSet();

            var recipesQuery = _context.Recipes
                .AsNoTracking()
                .Where(r => r.CreatedByUserId == null || r.CreatedByUserId == userId);

            if (filter.DietId.HasValue && filter.DietId.Value != Guid.Empty)
            {
                recipesQuery = recipesQuery.Where(r => r.RecipeDiets.Any(rd => rd.DietId == filter.DietId.Value));
            }
            else if (!string.IsNullOrWhiteSpace(filter.Diet))
            {
                var dietSlug = filter.Diet.Trim().ToLowerInvariant();
                if (dietSlug != "standard" && dietSlug != "0")
                {
                    recipesQuery = recipesQuery.Where(r => r.RecipeDiets.Any(rd => rd.Diet.Code.ToLower() == dietSlug));
                }
            }

            if (hasSelectedItemsFilter)
            {
                recipesQuery = recipesQuery.Where(r => r.Ingredients.Any(i => targetProductIds.Contains(i.ProductId)));
            }

            var scope = filter.Scope?.Trim().ToLowerInvariant();
            recipesQuery = scope switch
            {
                "favorites" => recipesQuery.Where(r => favRecipeIds.Contains(r.Id)),
                "custom" => recipesQuery.Where(r => r.CreatedByUserId == userId),
                _ => recipesQuery
            };

            if (!string.IsNullOrWhiteSpace(filter.Query))
            {
                var pattern = filter.Query.Trim().ToContainsPattern();
                recipesQuery = recipesQuery.Where(r =>
                    EF.Functions.ILike(r.Title, pattern) ||
                    r.Translations.Any(t => EF.Functions.ILike(t.Title, pattern)) ||
                    r.Ingredients.Any(i => EF.Functions.ILike(i.Product.Name, pattern) ||
                                           i.Product.Translations.Any(pt => EF.Functions.ILike(pt.Name, pattern)))
                );
            }

            if (filter.MaxTime > 0)
            {
                recipesQuery = recipesQuery.Where(r => r.PrepTimeMinutes <= filter.MaxTime.Value);
            }

            var matchingRecipes = await recipesQuery
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.ImageUrl,
                    r.PrepTimeMinutes,
                    r.Servings,
                    r.Calories,
                    r.CreatedByUserId,
                    Diets = r.RecipeDiets
                        .OrderBy(rd => rd.Diet.DisplayOrder)
                        .Select(rd => new DietDto
                        {
                            Id = rd.Diet.Id,
                            Code = rd.Diet.Code,
                            Name = rd.Diet.Translations
                                .Where(t => t.LanguageId == targetLangId)
                                .Select(t => t.Name)
                                .FirstOrDefault() ?? rd.Diet.Name
                        }).ToList(),
                    TranslatedTitle = r.Translations
                        .Where(t => t.LanguageId == targetLangId)
                        .Select(t => t.Title)
                        .FirstOrDefault(),
                    Ingredients = r.Ingredients.Select(i => new
                    {
                        i.ProductId,
                        i.IsOptional,
                        IsSpice = i.Product.Category.IsPantryStaple,
                        i.Quantity,
                        i.Unit,
                        ProductName = i.Product.Name,
                        TranslatedName = i.Product.Translations
                            .Where(pt => pt.LanguageId == targetLangId)
                            .Select(pt => pt.Name)
                            .FirstOrDefault()
                    }).ToList()
                })
                .ToListAsync();

            var processed = matchingRecipes.Select(recipe =>
            {
                var required = recipe.Ingredients.Where(i => !i.IsOptional && !i.IsSpice).ToList();

                var owned = required.Where(i => userProductIds.Contains(i.ProductId))
                                    .OrderByDescending(i => targetProductIds.Contains(i.ProductId))
                                    .ToList();

                var missing = required.Where(i => !userProductIds.Contains(i.ProductId))
                    .Select(i => new MissingIngredientDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.TranslatedName ?? i.ProductName ?? "Składnik",
                        Quantity = i.Quantity,
                        Unit = i.Unit
                    }).ToList();

                userInteractions.TryGetValue(recipe.Id, out var interaction);
                var total = required.Count;
                var ownedCount = owned.Count;

                return new
                {
                    Dto = new RecipeMatchDto
                    {
                        RecipeId = recipe.Id,
                        Title = recipe.TranslatedTitle ?? recipe.Title,
                        ImageUrl = recipe.ImageUrl,
                        PrepTimeMinutes = recipe.PrepTimeMinutes,
                        Servings = recipe.Servings,
                        Calories = recipe.Calories,
                        Diets = recipe.Diets,
                        MatchPercentage = total > 0 ? (int)Math.Round((double)ownedCount / total * 100) : 100,
                        OwnedIngredientsCount = ownedCount,
                        TotalIngredientsCount = total,
                        ExpiringIngredientsSaved = owned.Count(i => expiringProductIds.Contains(i.ProductId)),
                        CanBeCookedNow = missing.Count == 0,
                        IsFavorite = interaction?.IsFavorite ?? false,
                        IsCustom = recipe.CreatedByUserId == userId,
                        UsedIngredientsSummary = string.Join(", ", owned.Take(3).Select(i => i.TranslatedName ?? i.ProductName)),
                        MissingIngredients = missing
                    },
                    SelectedUsedCount = hasSelectedItemsFilter ? required.Count(i => targetProductIds.Contains(i.ProductId)) : 0
                };
            });

            var sortBy = filter.SortBy?.Trim().ToLowerInvariant();
            var sorted = hasSelectedItemsFilter
                ? processed.OrderByDescending(x => x.SelectedUsedCount)
                           .ThenByDescending(x => x.Dto.CanBeCookedNow)
                           .ThenByDescending(x => x.Dto.MatchPercentage)
                           .ThenBy(x => x.Dto.Title)
                           .Select(x => x.Dto)
                : sortBy switch
                {
                    "time" => processed.OrderBy(x => x.Dto.PrepTimeMinutes).Select(x => x.Dto),
                    "calories" => processed.OrderBy(x => x.Dto.Calories ?? int.MaxValue).Select(x => x.Dto),
                    _ => processed.OrderByDescending(x => x.Dto.CanBeCookedNow)
                                   .ThenByDescending(x => x.Dto.ExpiringIngredientsSaved)
                                   .ThenByDescending(x => x.Dto.MatchPercentage)
                                   .ThenBy(x => x.Dto.Title)
                                   .Select(x => x.Dto)
                };

            var list = sorted.ToList();

            return ServiceResult<PagedResult<RecipeMatchDto>>.Success(new PagedResult<RecipeMatchDto>
            {
                Items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = list.Count,
                Page = page,
                PageSize = pageSize
            });
        }
        public async Task<ServiceResult<RecipeDetailDto>> GetByIdAsync(Guid recipeId, string lang = "pl")
        {
            var userId = _currentUserService.UserId;
            var normalizedLang = (lang ?? "pl").Trim().ToLowerInvariant();
            var targetLangId = await _cache.GetLanguageIdAsync(_context, $"lang:recipes:{normalizedLang}", l => l.Code.ToLower() == normalizedLang);

            var recipe = await _context.Recipes
                .AsNoTracking()
                .Where(r => r.Id == recipeId && (r.CreatedByUserId == null || r.CreatedByUserId == userId))
                .Select(r => new RecipeDetailDto
                {
                    Id = r.Id,
                    Title = r.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Title).FirstOrDefault() ?? r.Title,
                    Instructions = r.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Instructions).FirstOrDefault() ?? r.Instructions,
                    ImageUrl = r.ImageUrl,
                    PrepTimeMinutes = r.PrepTimeMinutes,
                    Servings = r.Servings,
                    Calories = r.Calories,
                    Diets = r.RecipeDiets
                        .OrderBy(rd => rd.Diet.DisplayOrder)
                        .Select(rd => new DietDto
                        {
                            Id = rd.Diet.Id,
                            Code = rd.Diet.Code,
                            Name = rd.Diet.Translations.Where(t => t.LanguageId == targetLangId).Select(t => t.Name).FirstOrDefault() ?? rd.Diet.Name
                        }).ToList(),
                    IsFavorite = _context.UserRecipeInteractions.Any(x => x.UserId == userId && x.RecipeId == r.Id && x.IsFavorite),
                    UserVote = _context.UserRecipeInteractions.Where(x => x.UserId == userId && x.RecipeId == r.Id).Select(x => x.Vote).FirstOrDefault(),
                    LikesCount = _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote == true),
                    DislikesCount = _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote == false),
                    IsCustom = r.CreatedByUserId == userId,
                    Ingredients = r.Ingredients.Select(i => new RecipeIngredientDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product.Translations.Where(pt => pt.LanguageId == targetLangId).Select(pt => pt.Name).FirstOrDefault() ?? i.Product.Name,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        IsOptional = i.IsOptional,
                        IsOwnedInPantry = _context.PantryItems.Any(pi => pi.UserId == userId && pi.Status == PantryItemStatus.Active && pi.ProductId == i.ProductId)
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            return recipe != null
                ? ServiceResult<RecipeDetailDto>.Success(recipe)
                : ServiceResult<RecipeDetailDto>.Failure("Nie znaleziono wybranego przepisu.");
        }

        public async Task<ServiceResult<bool>> ToggleFavoriteAsync(Guid recipeId)
        {
            var userId = _currentUserService.UserId;

            var recipeExists = await _context.Recipes
                .AnyAsync(r => r.Id == recipeId && (r.CreatedByUserId == null || r.CreatedByUserId == userId));

            if (!recipeExists)
                return ServiceResult<bool>.Failure("Przepis nie istnieje.");

            var interaction = await _context.UserRecipeInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.RecipeId == recipeId);

            if (interaction == null)
            {
                _context.UserRecipeInteractions.Add(new UserRecipeInteraction
                {
                    UserId = userId,
                    RecipeId = recipeId,
                    IsFavorite = true,
                    FavoritedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return ServiceResult<bool>.Success(true);
            }

            interaction.IsFavorite = !interaction.IsFavorite;
            interaction.FavoritedAt = interaction.IsFavorite ? DateTime.UtcNow : null;
            interaction.UpdatedAt = DateTime.UtcNow;

            if (!interaction.IsFavorite && interaction.Vote == null)
                _context.UserRecipeInteractions.Remove(interaction);

            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Success(interaction.IsFavorite);
        }

        public async Task<ServiceResult<VoteResultDto>> VoteRecipeAsync(Guid recipeId, bool isLike)
        {
            var userId = _currentUserService.UserId;

            var isOfficialRecipe = await _context.Recipes
                .AnyAsync(r => r.Id == recipeId && r.CreatedByUserId == null);

            if (!isOfficialRecipe)
                return ServiceResult<VoteResultDto>.Failure("Głosowanie jest dostępne wyłącznie dla oficjalnych dań systemowych.");

            var interaction = await _context.UserRecipeInteractions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.RecipeId == recipeId);

            if (interaction == null)
            {
                interaction = new UserRecipeInteraction { UserId = userId, RecipeId = recipeId };
                _context.UserRecipeInteractions.Add(interaction);
            }

            interaction.Vote = interaction.Vote == isLike ? null : isLike;
            interaction.VotedAt = interaction.Vote.HasValue ? DateTime.UtcNow : null;
            interaction.UpdatedAt = DateTime.UtcNow;

            if (!interaction.IsFavorite && interaction.Vote == null)
                _context.UserRecipeInteractions.Remove(interaction);

            await _context.SaveChangesAsync();

            var votes = await _context.UserRecipeInteractions
                .AsNoTracking()
                .Where(x => x.RecipeId == recipeId && x.Vote != null)
                .Select(x => x.Vote)
                .ToListAsync();

            return ServiceResult<VoteResultDto>.Success(new VoteResultDto
            {
                LikesCount = votes.Count(v => v == true),
                DislikesCount = votes.Count(v => v == false),
                UserVote = interaction.Vote
            });
        }

        public async Task<ServiceResult<IEnumerable<DeductedIngredientDto>>> CookRecipeAsync(Guid recipeId, string lang = "pl")
        {
            var userId = _currentUserService.UserId;
            var normalizedLang = (lang ?? "pl").Trim().ToLowerInvariant();
            var targetLangId = await _cache.GetLanguageIdAsync(_context, $"lang:recipes:{normalizedLang}", l => l.Code.ToLower() == normalizedLang);

            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.Ingredients)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Category)
                .Include(r => r.Ingredients)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Translations)
                .FirstOrDefaultAsync(r => r.Id == recipeId && (r.CreatedByUserId == null || r.CreatedByUserId == userId));

            if (recipe == null)
                return ServiceResult<IEnumerable<DeductedIngredientDto>>.Failure("Nie znaleziono wybranego przepisu.");

            var ingredientsToDeduct = recipe.Ingredients
                .Where(i => !i.IsOptional && !i.Product.Category.IsPantryStaple)
                .ToList();

            if (!ingredientsToDeduct.Any())
                return ServiceResult<IEnumerable<DeductedIngredientDto>>.Success(Enumerable.Empty<DeductedIngredientDto>());

            var productIds = ingredientsToDeduct.Select(i => i.ProductId).Distinct().ToList();

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var pantryItems = await _context.PantryItems
                        .Where(pi => pi.UserId == userId && pi.Status == PantryItemStatus.Active && productIds.Contains(pi.ProductId))
                        .OrderBy(pi => pi.ExpirationDate)
                        .ThenBy(pi => pi.CreatedAt)
                        .ToListAsync();

                    var deductedResults = new List<DeductedIngredientDto>();

                    foreach (var ingredient in ingredientsToDeduct)
                    {
                        var matchingPantry = pantryItems.Where(pi => pi.ProductId == ingredient.ProductId && pi.Quantity > 0).ToList();
                        if (!matchingPantry.Any()) continue;

                        var remainingNeeded = ingredient.Quantity;
                        decimal totalDeducted = 0;

                        foreach (var item in matchingPantry)
                        {
                            if (remainingNeeded <= 0) break;

                            var toDeduct = Math.Min(item.Quantity, remainingNeeded);
                            item.Quantity -= toDeduct;
                            item.UpdatedAt = DateTime.UtcNow;

                            if (item.Quantity <= 0)
                            {
                                item.Quantity = 0;
                                item.Status = PantryItemStatus.Consumed;
                                item.ResolvedAt = DateTime.UtcNow;
                            }

                            remainingNeeded -= toDeduct;
                            totalDeducted += toDeduct;
                        }

                        if (totalDeducted > 0)
                        {
                            var name = ingredient.Product.Translations.FirstOrDefault(t => t.LanguageId == targetLangId)?.Name
                                       ?? ingredient.Product.Name;

                            deductedResults.Add(new DeductedIngredientDto
                            {
                                ProductId = ingredient.ProductId,
                                ProductName = name,
                                Quantity = totalDeducted,
                                Unit = ingredient.Product.Unit
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return ServiceResult<IEnumerable<DeductedIngredientDto>>.Success(deductedResults);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Błąd podczas odliczania składników dla przepisu {RecipeId}", recipeId);
                    return ServiceResult<IEnumerable<DeductedIngredientDto>>.Failure("Wystąpił błąd podczas aktualizacji spiżarni.");
                }
            });
        }
        public async Task<ServiceResult<IEnumerable<DietDto>>> GetDietsAsync(string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLowerInvariant();

            var diets = await _context.Diets
                .AsNoTracking()
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new DietDto
                {
                    Id = d.Id,
                    Code = d.Code,
                    Name = d.Translations
                        .Where(t => t.Language.Code.ToLower() == normalizedLang)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? d.Name
                })
                .ToListAsync();

            return ServiceResult<IEnumerable<DietDto>>.Success(diets);
        }

    }
}