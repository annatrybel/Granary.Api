using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Granary.Api.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<RecipeService> _logger;
        private readonly IFileStorageService _fileStorageService;

        public RecipeService(GranaryDbContext context, ILogger<RecipeService> logger, IFileStorageService fileStorageService)
        {
            _context = context;
            _logger = logger;
            _fileStorageService = fileStorageService;
        }

        public async Task<ServiceResult<PagedRecipesResultDto>> GetFilteredRecipesAsync(Guid userId, RecipeFilterQueryDto filter)
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

            var lang = string.IsNullOrWhiteSpace(filter.Lang) ? "pl" : filter.Lang.Trim().ToLower();
            var targetLang = await _context.Languages.AsNoTracking().FirstOrDefaultAsync(l => l.Code.ToLower() == lang);
            var targetLangId = targetLang?.Id ?? Guid.Empty;

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
                .Include(r => r.Translations)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Product)
                        .ThenInclude(p => p.Translations)
                .AsQueryable();

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
                var normalized = filter.Query.Trim().ToLower();
                recipesQuery = recipesQuery.Where(r =>
                    r.Title.ToLower().Contains(normalized) ||
                    r.Translations.Any(t => t.Title.ToLower().Contains(normalized)) ||
                    (r.Description != null && r.Description.ToLower().Contains(normalized)) ||
                    r.Translations.Any(t => t.Description != null && t.Description.ToLower().Contains(normalized)) ||
                    r.Ingredients.Any(i =>
                        i.Product.Name.ToLower().Contains(normalized) ||
                        i.Product.Translations.Any(pt => pt.Name.ToLower().Contains(normalized)))
                );
            }

            if (filter.MaxTime.HasValue && filter.MaxTime.Value > 0)
            {
                recipesQuery = recipesQuery.Where(r => r.PrepTimeMinutes <= filter.MaxTime.Value);
            }

            var matchingRecipes = await recipesQuery.ToListAsync();

            // 3. Mapowanie DTO z uwzględnieniem tłumaczenia
            var processed = matchingRecipes.Select(recipe =>
            {
                var trans = recipe.Translations.FirstOrDefault(t => t.LanguageId == targetLangId);
                var localizedTitle = trans?.Title ?? recipe.Title;
                var localizedDesc = trans?.Description ?? recipe.Description;

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
                        ProductName = i.Product?.Translations.FirstOrDefault(pt => pt.LanguageId == targetLangId)?.Name
                                      ?? i.Product?.Name
                                      ?? "Składnik",
                        Quantity = i.Quantity,
                        Unit = i.Unit
                    })
                    .ToList();

                var ownedCount = owned.Count;
                var matchPct = totalCount > 0 ? (int)Math.Round((double)ownedCount / totalCount * 100) : 100;
                var expiringSaved = owned.Count(i => expiringProductIds.Contains(i.ProductId));

                var summary = string.Join(", ", owned.Take(3).Select(i =>
                    i.Product?.Translations.FirstOrDefault(pt => pt.LanguageId == targetLangId)?.Name
                    ?? i.Product?.Name));

                userInteractions.TryGetValue(recipe.Id, out var interaction);

                return new
                {
                    Dto = new RecipeMatchDto
                    {
                        RecipeId = recipe.Id,
                        Title = localizedTitle,
                        Description = localizedDesc,
                        ImageUrl = recipe.ImageUrl,
                        PrepTimeMinutes = recipe.PrepTimeMinutes,
                        Servings = recipe.Servings,
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
            var totalCount = list.Count;

            return ServiceResult<PagedRecipesResultDto>.Success(new PagedRecipesResultDto
            {
                Items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMore = (page * pageSize) < totalCount
            });
        }

        public async Task<ServiceResult<RecipeDetailDto>> GetByIdAsync(Guid recipeId, Guid userId, string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLower();
            var targetLang = await _context.Languages.AsNoTracking().FirstOrDefaultAsync(l => l.Code.ToLower() == normalizedLang);
            var targetLangId = targetLang?.Id ?? Guid.Empty;

            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.Translations)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Product)
                        .ThenInclude(p => p.Translations)
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

            // Pobieramy tłumaczenie przepisu
            var trans = recipe.Translations.FirstOrDefault(t => t.LanguageId == targetLangId);

            var detail = new RecipeDetailDto
            {
                Id = recipe.Id,
                Title = trans?.Title ?? recipe.Title,
                Description = trans?.Description ?? recipe.Description,
                Instructions = trans?.Instructions ?? recipe.Instructions,
                ImageUrl = recipe.ImageUrl,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                Servings = recipe.Servings,
                IsFavorite = userInteraction?.IsFavorite ?? false,
                IsCustom = recipe.CreatedByUserId == userId,
                LikesCount = allVotes.Count(v => v == true),
                DislikesCount = allVotes.Count(v => v == false),
                UserVote = userInteraction?.Vote,
                Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Translations.FirstOrDefault(pt => pt.LanguageId == targetLangId)?.Name
                                  ?? i.Product?.Name
                                  ?? "Składnik",
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

        public async Task<ServiceResult<RecipeDetailDto>> CreateCustomRecipeAsync(CreateCustomRecipeDto dto, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return ServiceResult<RecipeDetailDto>.Failure("Nazwa przepisu nie może być pusta.");
            }

            string? persistentImageUrl = dto.ImageUrl;
            if (!string.IsNullOrWhiteSpace(dto.ImageUrl) && dto.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var localUrl = await _fileStorageService.SaveImageFromUrlAsync(dto.ImageUrl, "recipes");
                if (!string.IsNullOrEmpty(localUrl))
                {
                    persistentImageUrl = localUrl;
                }
            }

            var instructions = dto.Steps != null && dto.Steps.Any()
                ? string.Join("\n", dto.Steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()))
                : "Brak szczegółowej instrukcji.";

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Description = !string.IsNullOrWhiteSpace(dto.Description)
                    ? dto.Description.Trim()
                    : (!string.IsNullOrWhiteSpace(dto.Category) ? $"Kategoria: {dto.Category}" : null),
                ImageUrl = persistentImageUrl, 
                Instructions = instructions,
                PrepTimeMinutes = dto.PrepTimeMinutes > 0 ? dto.PrepTimeMinutes : 15,
                Servings = dto.Servings > 0 ? dto.Servings : 1,
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
                Description = recipe.Description,
                Instructions = recipe.Instructions,
                ImageUrl = recipe.ImageUrl,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                Servings = recipe.Servings,
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

        public async Task<ServiceResult<bool>> DeleteRecipeAsync(Guid recipeId, Guid userId)
        {
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

            // Przed _context.Recipes.Remove(recipe);
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

        public async Task<ServiceResult<RecipeDetailDto>> UpdateCustomRecipeAsync(Guid recipeId, CreateCustomRecipeDto dto, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return ServiceResult<RecipeDetailDto>.Failure("Nazwa przepisu nie może być pusta.");
            }

            var recipe = await _context.Recipes
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
            {
                return ServiceResult<RecipeDetailDto>.Failure("Przepis nie istnieje.");
            }

            if (recipe.CreatedByUserId != userId)
            {
                return ServiceResult<RecipeDetailDto>.Failure("Brak uprawnień do edycji tego przepisu.");
            }

            if (!string.IsNullOrWhiteSpace(dto.ImageUrl) && dto.ImageUrl != recipe.ImageUrl)
            {
                if (dto.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !dto.ImageUrl.Contains("/uploads/"))
                {
                    _fileStorageService.DeleteFile(recipe.ImageUrl);

                    var localUrl = await _fileStorageService.SaveImageFromUrlAsync(dto.ImageUrl, "recipes");
                    recipe.ImageUrl = localUrl ?? dto.ImageUrl;
                }
                else
                {
                    recipe.ImageUrl = dto.ImageUrl;
                }
            }

            var instructions = dto.Steps != null && dto.Steps.Any()
                ? string.Join("\n", dto.Steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()))
                : recipe.Instructions;

            recipe.Title = dto.Title.Trim();
            recipe.Description = !string.IsNullOrWhiteSpace(dto.Category) ? $"Kategoria: {dto.Category}" : dto.Description;
            recipe.Instructions = instructions;
            recipe.PrepTimeMinutes = dto.PrepTimeMinutes > 0 ? dto.PrepTimeMinutes : recipe.PrepTimeMinutes;
            recipe.Servings = dto.Servings > 0 ? dto.Servings : recipe.Servings;
            if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
            {
                recipe.ImageUrl = dto.ImageUrl;
            }

            var oldIngredients = await _context.RecipeIngredients
                .Where(ri => ri.RecipeId == recipeId)
                .ToListAsync();
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

            return await GetByIdAsync(recipe.Id, userId);
        }
    }
}