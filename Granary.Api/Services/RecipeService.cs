using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Granary.Api.Extensions;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Granary.Api.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<RecipeService> _logger;
        private readonly IFileStorageService _fileStorageService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMemoryCache _cache;

        public RecipeService(
            GranaryDbContext context,
            ILogger<RecipeService> logger,
            IFileStorageService fileStorageService,
            ICurrentUserService currentUserService,
            IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _fileStorageService = fileStorageService;
            _currentUserService = currentUserService;
            _cache = cache;
        }

        public async Task<ServiceResult<PagedResult<RecipeMatchDto>>> GetFilteredRecipesAsync(RecipeFilterQueryDto filter)
        {
            var userId = _currentUserService.UserId;
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

            var lang = string.IsNullOrWhiteSpace(filter.Lang) ? "pl" : filter.Lang.Trim().ToLower();
            var targetLangId = await _cache.GetLanguageIdAsync(_context, $"lang:recipes:{lang}", l => l.Code.ToLower() == lang);

            var userPantry = await _context.PantryItems
                .AsNoTracking()
                .Where(pi => pi.UserId == userId && pi.Status == PantryItemStatus.Active)
                .Select(pi => new
                {
                    pi.ProductId,
                    IsExpiringSoon = pi.ExpirationDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))
                })
                .ToListAsync();

            var userProductIds = userPantry.Select(p => p.ProductId).ToHashSet();
            var expiringProductIds = userPantry.Where(p => p.IsExpiringSoon).Select(p => p.ProductId).ToHashSet();

            HashSet<Guid> targetProductIds = new();
            bool hasSelectedItemsFilter = false;

            if (!string.IsNullOrWhiteSpace(filter.Items))
            {
                var parsedIds = filter.Items
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToHashSet();

                if (parsedIds.Any())
                {
                    targetProductIds = parsedIds;
                    hasSelectedItemsFilter = true;
                }
            }

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
                .AsQueryable();

            var targetDiet = filter.Diet;

            if (!targetDiet.HasValue)
            {
                targetDiet = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => (DietaryPreference?)u.DietaryPreference)
                    .FirstOrDefaultAsync();
            }

            if (targetDiet.HasValue && targetDiet.Value != DietaryPreference.Standard)
            {
                recipesQuery = targetDiet.Value switch
                {
                    DietaryPreference.Vegan =>
                        recipesQuery.Where(r => r.DietaryPreference == DietaryPreference.Vegan),

                    DietaryPreference.Vegetarian =>
                        recipesQuery.Where(r => r.DietaryPreference == DietaryPreference.Vegetarian
                                             || r.DietaryPreference == DietaryPreference.Vegan),

                    DietaryPreference.Pescetarian =>
                        recipesQuery.Where(r => r.DietaryPreference == DietaryPreference.Pescetarian
                                             || r.DietaryPreference == DietaryPreference.Vegetarian
                                             || r.DietaryPreference == DietaryPreference.Vegan),

                    DietaryPreference.Keto =>
                        recipesQuery.Where(r => r.DietaryPreference == DietaryPreference.Keto),

                    _ => recipesQuery
                };
            }

            if (hasSelectedItemsFilter)
            {
                recipesQuery = recipesQuery.Where(r => r.Ingredients.Any(i => targetProductIds.Contains(i.ProductId)));
            }

            var scope = filter.Scope?.ToLower().Trim();
            if (scope == "favorites") recipesQuery = recipesQuery.Where(r => favRecipeIds.Contains(r.Id));
            else if (scope == "custom") recipesQuery = recipesQuery.Where(r => r.CreatedByUserId == userId);
            else recipesQuery = recipesQuery.Where(r => r.CreatedByUserId == null || r.CreatedByUserId == userId);

            if (!string.IsNullOrWhiteSpace(filter.Query))
            {
                var pattern = filter.Query.Trim().ToContainsPattern();
                recipesQuery = recipesQuery.Where(r =>
                    EF.Functions.ILike(r.Title, pattern) ||
                    r.Translations.Any(t => EF.Functions.ILike(t.Title, pattern)) ||
                    r.Ingredients.Any(i =>
                        EF.Functions.ILike(i.Product.Name, pattern) ||
                        i.Product.Translations.Any(pt => EF.Functions.ILike(pt.Name, pattern)))
                );
            }

            if (filter.MaxTime.HasValue && filter.MaxTime.Value > 0)
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
                    r.DietaryPreference,
                    r.CreatedByUserId,
                    TranslatedTitle = r.Translations
                        .Where(t => t.LanguageId == targetLangId)
                        .Select(t => t.Title)
                        .FirstOrDefault(),
                    Ingredients = r.Ingredients.Select(i => new
                    {
                        i.ProductId,
                        i.IsOptional,
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
                var localizedTitle = recipe.TranslatedTitle ?? recipe.Title;

                var requiredIngredients = recipe.Ingredients.Where(i => !i.IsOptional).ToList();
                var totalCount = requiredIngredients.Count;

                var selectedUsedCount = hasSelectedItemsFilter
                    ? requiredIngredients.Count(i => targetProductIds.Contains(i.ProductId))
                    : 0;

                var owned = requiredIngredients
                    .Where(i => userProductIds.Contains(i.ProductId))
                    .OrderByDescending(i => targetProductIds.Contains(i.ProductId))
                    .ToList();

                var missing = requiredIngredients
                    .Where(i => !userProductIds.Contains(i.ProductId))
                    .Select(i => new MissingIngredientDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.TranslatedName ?? i.ProductName ?? "Składnik",
                        Quantity = i.Quantity,
                        Unit = i.Unit
                    })
                    .ToList();

                var ownedCount = owned.Count;
                var matchPct = totalCount > 0 ? (int)Math.Round((double)ownedCount / totalCount * 100) : 100;
                var expiringSaved = owned.Count(i => expiringProductIds.Contains(i.ProductId));

                var summary = string.Join(", ", owned.Take(3).Select(i => i.TranslatedName ?? i.ProductName));

                userInteractions.TryGetValue(recipe.Id, out var interaction);

                return new
                {
                    Dto = new RecipeMatchDto
                    {
                        RecipeId = recipe.Id,
                        Title = localizedTitle,
                        ImageUrl = recipe.ImageUrl,
                        PrepTimeMinutes = recipe.PrepTimeMinutes,
                        Servings = recipe.Servings,
                        Calories = recipe.Calories,
                        DietaryPreference = recipe.DietaryPreference,
                        MatchPercentage = matchPct,
                        OwnedIngredientsCount = ownedCount,
                        TotalIngredientsCount = totalCount,
                        ExpiringIngredientsSaved = expiringSaved,
                        CanBeCookedNow = missing.Count == 0,
                        IsFavorite = interaction?.IsFavorite ?? false,
                        IsCustom = recipe.CreatedByUserId == userId,
                        UsedIngredientsSummary = summary,
                        MissingIngredients = missing
                    },
                    SelectedUsedCount = selectedUsedCount
                };
            });

            IEnumerable<RecipeMatchDto> sorted;
            if (hasSelectedItemsFilter)
            {
                sorted = processed
                    .OrderByDescending(x => x.SelectedUsedCount)
                    .ThenByDescending(x => x.Dto.CanBeCookedNow)
                    .ThenByDescending(x => x.Dto.MatchPercentage)
                    .ThenBy(x => x.Dto.Title)
                    .Select(x => x.Dto);
            }
            else
            {
                sorted = (filter.SortBy?.ToLower() == "time")
                    ? processed.OrderBy(x => x.Dto.PrepTimeMinutes).Select(x => x.Dto)
                    : processed
                        .OrderByDescending(x => x.Dto.CanBeCookedNow)
                        .ThenByDescending(x => x.Dto.ExpiringIngredientsSaved)
                        .ThenByDescending(x => x.Dto.MatchPercentage)
                        .ThenBy(x => x.Dto.Title)
                        .Select(x => x.Dto);
            }

            var list = sorted.ToList();
            var totalFilteredCount = list.Count;

            return ServiceResult<PagedResult<RecipeMatchDto>>.Success(new PagedResult<RecipeMatchDto>
            {
                Items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = totalFilteredCount,
                Page = page,
                PageSize = pageSize
            });
        }

        public async Task<ServiceResult<RecipeDetailDto>> GetByIdAsync(Guid recipeId, string lang = "pl")
        {
            var userId = _currentUserService.UserId;
            var normalizedLang = (lang ?? "pl").Trim().ToLower();
            var targetLangId = await _cache.GetLanguageIdAsync(_context, $"lang:recipes:{normalizedLang}", l => l.Code.ToLower() == normalizedLang);

            var recipe = await _context.Recipes
                .AsNoTracking()
                .Where(r => r.Id == recipeId)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Instructions,
                    r.ImageUrl,
                    r.PrepTimeMinutes,
                    r.Servings,
                    r.Calories,
                    r.DietaryPreference,
                    r.CreatedByUserId,
                    TranslatedTitle = r.Translations
                        .Where(t => t.LanguageId == targetLangId)
                        .Select(t => t.Title)
                        .FirstOrDefault(),
                    TranslatedInstructions = r.Translations
                        .Where(t => t.LanguageId == targetLangId)
                        .Select(t => t.Instructions)
                        .FirstOrDefault(),
                    IsFavorite = _context.UserRecipeInteractions
                        .Any(x => x.UserId == userId && x.RecipeId == r.Id && x.IsFavorite),
                    UserVote = _context.UserRecipeInteractions
                        .Where(x => x.UserId == userId && x.RecipeId == r.Id)
                        .Select(x => x.Vote)
                        .FirstOrDefault(),
                    LikesCount = _context.UserRecipeInteractions
                        .Count(x => x.RecipeId == r.Id && x.Vote == true),
                    DislikesCount = _context.UserRecipeInteractions
                        .Count(x => x.RecipeId == r.Id && x.Vote == false),
                    Ingredients = r.Ingredients.Select(i => new
                    {
                        i.ProductId,
                        ProductName = i.Product.Name,
                        TranslatedName = i.Product.Translations
                            .Where(pt => pt.LanguageId == targetLangId)
                            .Select(pt => pt.Name)
                            .FirstOrDefault(),
                        i.Quantity,
                        i.Unit,
                        i.IsOptional,
                        IsOwnedInPantry = _context.PantryItems.Any(pi =>
                            pi.UserId == userId
                            && pi.Status == PantryItemStatus.Active
                            && pi.ProductId == i.ProductId)
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (recipe == null)
            {
                return ServiceResult<RecipeDetailDto>.Failure("Nie znaleziono wybranego przepisu.");
            }

            var detail = new RecipeDetailDto
            {
                Id = recipe.Id,
                Title = recipe.TranslatedTitle ?? recipe.Title,
                Instructions = recipe.TranslatedInstructions ?? recipe.Instructions,
                ImageUrl = recipe.ImageUrl,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                Servings = recipe.Servings,
                Calories = recipe.Calories,
                DietaryPreference = recipe.DietaryPreference,
                IsFavorite = recipe.IsFavorite,
                IsCustom = recipe.CreatedByUserId == userId,
                LikesCount = recipe.LikesCount,
                DislikesCount = recipe.DislikesCount,
                UserVote = recipe.UserVote,
                Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.TranslatedName ?? i.ProductName ?? "Składnik",
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    IsOptional = i.IsOptional,
                    IsOwnedInPantry = i.IsOwnedInPantry
                }).ToList()
            };

            return ServiceResult<RecipeDetailDto>.Success(detail);
        }

        public async Task<ServiceResult<bool>> ToggleFavoriteAsync(Guid recipeId)
        {
            var userId = _currentUserService.UserId;
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

        public async Task<ServiceResult<VoteResultDto>> VoteRecipeAsync(Guid recipeId, bool isLike)
        {
            var userId = _currentUserService.UserId;
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

        public async Task<ServiceResult<RecipeDetailDto>> CreateCustomRecipeAsync(CreateCustomRecipeDto dto)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return ServiceResult<RecipeDetailDto>.Failure("Nazwa przepisu nie może być pusta.");
            }

            string? persistentImageUrl = await _fileStorageService.ProcessImageAsync(dto.ImageUrl, null, "recipes");

            var instructions = dto.Steps != null && dto.Steps.Any()
                ? string.Join("\n", dto.Steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()))
                : "Brak szczegółowej instrukcji.";

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                ImageUrl = persistentImageUrl,
                Instructions = instructions,
                PrepTimeMinutes = dto.PrepTimeMinutes > 0 ? dto.PrepTimeMinutes : 15,
                Servings = dto.Servings > 0 ? dto.Servings : 1,
                Calories = dto.Calories,
                DietaryPreference = dto.DietaryPreference,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            var defaultCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == "inne")
                                  ?? await _context.Categories.FirstOrDefaultAsync();

            foreach (var ingDto in dto.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i.Name)))
            {
                var (parsedQty, parsedUnit) = ParseQuantityAndUnit(ingDto.Quantity);
                Product? product = null;

                if (ingDto.ProductId.HasValue && ingDto.ProductId.Value != Guid.Empty)
                {
                    product = await _context.Products.FirstOrDefaultAsync(p => p.Id == ingDto.ProductId.Value);
                }

                if (product == null)
                {
                    var normalizedName = ingDto.Name.Trim().ToLower();
                    product = await _context.Products.FirstOrDefaultAsync(p => p.Name.ToLower() == normalizedName);

                    if (product == null)
                    {
                        product = new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = ingDto.Name.Trim(),
                            CategoryId = defaultCategory?.Id ?? Guid.NewGuid(),
                            DefaultUnit = parsedUnit,
                            CreatedByUserId = userId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Products.Add(product);
                        await _context.SaveChangesAsync();
                    }
                }

                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    ProductId = product.Id,
                    Quantity = parsedQty,
                    Unit = parsedUnit,
                    IsOptional = false
                });
            }

            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();

            var detailDto = new RecipeDetailDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Instructions = recipe.Instructions,
                ImageUrl = recipe.ImageUrl,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                Servings = recipe.Servings,
                Calories = recipe.Calories,
                DietaryPreference = recipe.DietaryPreference,
                IsFavorite = true,
                LikesCount = 0,
                DislikesCount = 0,
                UserVote = null,
                Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "Składnik",
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    IsOptional = false,
                    IsOwnedInPantry = true
                }).ToList()
            };

            return ServiceResult<RecipeDetailDto>.Success(detailDto);
        }

        public async Task<ServiceResult<bool>> DeleteRecipeAsync(Guid recipeId)
        {
            var userId = _currentUserService.UserId;
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
            {
                return ServiceResult<bool>.Failure("Przepis nie istnieje.");
            }

            if (recipe.CreatedByUserId != userId)
            {
                return ServiceResult<bool>.Failure("Brak uprawnień do usunięcia tego przepisu.");
            }

            if (!string.IsNullOrWhiteSpace(recipe.ImageUrl))
            {
                _fileStorageService.DeleteFile(recipe.ImageUrl);
            }

            var interactions = await _context.UserRecipeInteractions.Where(x => x.RecipeId == recipeId).ToListAsync();
            _context.UserRecipeInteractions.RemoveRange(interactions);
            _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
            _context.Recipes.Remove(recipe);

            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<RecipeDetailDto>> UpdateCustomRecipeAsync(Guid recipeId, CreateCustomRecipeDto dto)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return ServiceResult<RecipeDetailDto>.Failure("Nazwa przepisu nie może być pusta.");
            }

            var recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId);
            if (recipe == null)
            {
                return ServiceResult<RecipeDetailDto>.Failure("Przepis nie istnieje.");
            }

            if (recipe.CreatedByUserId != userId)
            {
                return ServiceResult<RecipeDetailDto>.Failure("Brak uprawnień do edycji tego przepisu.");
            }

            recipe.ImageUrl = await _fileStorageService.ProcessImageAsync(dto.ImageUrl, recipe.ImageUrl, "recipes");

            var instructions = dto.Steps != null && dto.Steps.Any()
                ? string.Join("\n", dto.Steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()))
                : recipe.Instructions;

            recipe.Title = dto.Title.Trim();
            recipe.Instructions = instructions;
            recipe.PrepTimeMinutes = dto.PrepTimeMinutes > 0 ? dto.PrepTimeMinutes : recipe.PrepTimeMinutes;
            recipe.Servings = dto.Servings > 0 ? dto.Servings : recipe.Servings;
            recipe.Calories = dto.Calories;
            recipe.DietaryPreference = dto.DietaryPreference;

            var oldIngredients = await _context.RecipeIngredients.Where(ri => ri.RecipeId == recipeId).ToListAsync();
            _context.RecipeIngredients.RemoveRange(oldIngredients);

            var defaultCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == "inne")
                                  ?? await _context.Categories.FirstOrDefaultAsync();
            var defaultCategoryId = defaultCategory?.Id ?? Guid.NewGuid();

            var newIngredients = new List<RecipeIngredient>();

            foreach (var ingDto in dto.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i.Name)))
            {
                var (parsedQty, parsedUnit) = ParseQuantityAndUnit(ingDto.Quantity);
                Product? product = null;

                if (ingDto.ProductId.HasValue && ingDto.ProductId.Value != Guid.Empty)
                {
                    product = await _context.Products.FirstOrDefaultAsync(p => p.Id == ingDto.ProductId.Value);
                }

                if (product == null)
                {
                    var normalizedName = ingDto.Name.Trim().ToLower();
                    product = await _context.Products.FirstOrDefaultAsync(p =>
                        p.Name.ToLower() == normalizedName &&
                        (p.CreatedByUserId == null || p.CreatedByUserId == userId));

                    if (product == null)
                    {
                        product = new Product
                        {
                            Id = Guid.NewGuid(),
                            Name = ingDto.Name.Trim(),
                            CategoryId = defaultCategoryId,
                            DefaultUnit = parsedUnit,
                            CreatedByUserId = userId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Products.Add(product);
                    }
                }

                newIngredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    ProductId = product.Id,
                    Quantity = parsedQty,
                    Unit = parsedUnit,
                    IsOptional = false
                });
            }

            await _context.RecipeIngredients.AddRangeAsync(newIngredients);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(recipe.Id);
        }

        private static (decimal quantity, string unit) ParseQuantityAndUnit(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return (1m, "szt.");
            input = input.Trim().Replace(',', '.');
            var match = Regex.Match(input, @"^([0-9]+(?:\.[0-9]+)?)\s*(.*)$");

            if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty))
            {
                var unit = match.Groups[2].Value.Trim();
                return (qty, string.IsNullOrEmpty(unit) ? "szt." : unit);
            }

            return (1m, "szt.");
        }
    }
}