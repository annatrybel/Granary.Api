using Granary.Api.Extensions;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Granary.Api.Services.Recipes
{
    public interface IAdminRecipeService
    {
        Task<ServiceResult<AdminRecipeKpisDto>> GetKpisAsync();
        Task<ServiceResult<PagedResult<AdminRecipeListItemDto>>> GetRecipesAsync(AdminRecipeFilterDto filter, string lang = "pl");
        Task<ServiceResult<AdminRecipeEditDto>> GetForEditAsync(Guid recipeId);
        Task<ServiceResult<AdminRecipeEditDto>> CreateAsync(AdminRecipeEditDto dto);
        Task<ServiceResult<AdminRecipeEditDto>> UpdateAsync(Guid recipeId, AdminRecipeEditDto dto);
        Task<ServiceResult<bool>> DeleteAsync(Guid recipeId);
    }

    public class AdminRecipeService : IAdminRecipeService
    {
        private readonly GranaryDbContext _context;
        private readonly IFileStorageService _fileStorageService;

        public AdminRecipeService(GranaryDbContext context, IFileStorageService fileStorageService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
        }
            
        public async Task<ServiceResult<AdminRecipeKpisDto>> GetKpisAsync()
        {
            var officialCount = await _context.Recipes.CountAsync(r => r.CreatedByUserId == null);
            var communityCount = await _context.Recipes.CountAsync(r => r.CreatedByUserId != null);

            var totalLikes = await _context.UserRecipeInteractions
                .CountAsync(x => x.Vote == true && x.Recipe.CreatedByUserId == null);

            var totalDislikes = await _context.UserRecipeInteractions
                .CountAsync(x => x.Vote == false && x.Recipe.CreatedByUserId == null);

            var totalVotes = totalLikes + totalDislikes;
            double? approvalPercentage = totalVotes > 0
                ? Math.Round((double)totalLikes / totalVotes * 100, 1)
                : null;

            var totalFavorites = await _context.UserRecipeInteractions
                .CountAsync(x => x.IsFavorite && x.Recipe.CreatedByUserId == null);
            var needsAttentionCount = await _context.Recipes
                .Where(r => r.CreatedByUserId == null &&
                            _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote != null) >= 3 &&
                            _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote == false) >
                            _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote == true))
                .CountAsync();

            return ServiceResult<AdminRecipeKpisDto>.Success(new AdminRecipeKpisDto
            {
                OfficialRecipesCount = officialCount,
                CommunityRecipesCount = communityCount,
                TotalLikes = totalLikes,
                TotalDislikes = totalDislikes,
                ApprovalPercentage = approvalPercentage,
                TotalFavoritesCount = totalFavorites,
                NeedsAttentionCount = needsAttentionCount
            });
        }

        public async Task<ServiceResult<PagedResult<AdminRecipeListItemDto>>> GetRecipesAsync(AdminRecipeFilterDto filter, string lang = "pl")
        {
            var normalizedLang = (lang ?? "pl").Trim().ToLowerInvariant();

            var query = _context.Recipes
                .AsNoTracking()
                .Where(r => r.CreatedByUserId == null);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var pattern = filter.Search.Trim().ToContainsPattern();
                query = query.Where(r =>
                    EF.Functions.ILike(r.Title, pattern) ||
                    r.Ingredients.Any(i => EF.Functions.ILike(i.Product.Name, pattern)));
            }

            if (filter.DietId.HasValue && filter.DietId.Value != Guid.Empty)
            {
                query = query.Where(r => r.RecipeDiets.Any(rd => rd.DietId == filter.DietId.Value));
            }

            if (filter.NeedsAttention == true)
            {
                query = query.Where(r =>
                    _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote != null) >= 3 &&
                    _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote == false) >
                    _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote == true));
            }

            var totalCount = await query.CountAsync();
            var page = Math.Max(1, filter.PageNumber);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);

            var rawItems = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.ImageUrl,
                    r.PrepTimeMinutes,
                    r.Servings,
                    r.Calories,
                    IngredientsCount = r.Ingredients.Count,
                    r.CreatedAt,
                    Diets = r.RecipeDiets
                        .OrderBy(rd => rd.Diet.DisplayOrder)
                        .Select(rd => new DietDto
                        {
                            Id = rd.Diet.Id,
                            Code = rd.Diet.Code,
                            Name = rd.Diet.Translations
                                .Where(t => t.Language.Code.ToLower() == normalizedLang)
                                .Select(t => t.Name)
                                .FirstOrDefault() ?? rd.Diet.Name
                        }).ToList(),
                    LikesCount = _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote == true),
                    DislikesCount = _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.Vote == false),
                    FavoritesCount = _context.UserRecipeInteractions.Count(x => x.RecipeId == r.Id && x.IsFavorite)
                })
                .ToListAsync();

            var items = rawItems.Select(r =>
            {
                var totalVotes = r.LikesCount + r.DislikesCount;
                double? approval = totalVotes > 0
                    ? Math.Round((double)r.LikesCount / totalVotes * 100, 1)
                    : null;

                var needsAttention = totalVotes >= 3 && r.DislikesCount > r.LikesCount;

                return new AdminRecipeListItemDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    ImageUrl = r.ImageUrl,
                    PrepTimeMinutes = r.PrepTimeMinutes,
                    Servings = r.Servings,
                    Calories = r.Calories,
                    IngredientsCount = r.IngredientsCount,
                    Diets = r.Diets,
                    LikesCount = r.LikesCount,
                    DislikesCount = r.DislikesCount,
                    FavoritesCount = r.FavoritesCount,
                    ApprovalPercentage = approval,
                    NeedsAttention = needsAttention,
                    CreatedAt = r.CreatedAt
                };
            }).ToList();

            return ServiceResult<PagedResult<AdminRecipeListItemDto>>.Success(new PagedResult<AdminRecipeListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        public async Task<ServiceResult<AdminRecipeEditDto>> GetForEditAsync(Guid recipeId)
        {
            var recipe = await _context.Recipes
                .AsNoTracking()
                .Where(r => r.Id == recipeId && r.CreatedByUserId == null)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.ImageUrl,
                    r.Servings,
                    r.PrepTimeMinutes,
                    r.Calories,
                    r.Instructions,
                    DietIds = r.RecipeDiets.Select(rd => rd.DietId).ToList(),
                    Ingredients = r.Ingredients
                        .OrderBy(i => i.Product.Name)
                        .Select(i => new AdminRecipeIngredientDto
                        {
                            ProductId = i.ProductId,
                            ProductName = i.Product.Name,
                            Quantity = i.Quantity,
                            Unit = i.Unit,
                            IsOptional = i.IsOptional
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (recipe == null)
                return ServiceResult<AdminRecipeEditDto>.Failure("Oficjalny przepis nie istnieje.");

            return ServiceResult<AdminRecipeEditDto>.Success(new AdminRecipeEditDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                ImageUrl = recipe.ImageUrl,
                Servings = recipe.Servings,
                PrepTimeMinutes = recipe.PrepTimeMinutes,
                Calories = recipe.Calories,
                DietIds = recipe.DietIds,
                Steps = SplitSteps(recipe.Instructions),
                Ingredients = recipe.Ingredients
            });
        }

        public async Task<ServiceResult<AdminRecipeEditDto>> CreateAsync(AdminRecipeEditDto dto)
        {
            var error = await ValidateAsync(dto);
            if (error != null) return ServiceResult<AdminRecipeEditDto>.Failure(error);

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                CreatedByUserId = null, 
                CreatedAt = DateTime.UtcNow,
                ImageUrl = await _fileStorageService.ProcessImageAsync(dto.ImageUrl, null, "recipes")
            };

            ApplyContent(recipe, dto);
            recipe.Ingredients = BuildIngredients(recipe.Id, dto).ToList();

            if (dto.DietIds != null && dto.DietIds.Any())
            {
                recipe.RecipeDiets = dto.DietIds.Distinct().Select(dietId => new RecipeDiet
                {
                    RecipeId = recipe.Id,
                    DietId = dietId
                }).ToList();
            }

            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();

            return await GetForEditAsync(recipe.Id);
        }

        public async Task<ServiceResult<AdminRecipeEditDto>> UpdateAsync(Guid recipeId, AdminRecipeEditDto dto)
        {
            var error = await ValidateAsync(dto);
            if (error != null) return ServiceResult<AdminRecipeEditDto>.Failure(error);

            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.Translations)
                .Include(r => r.RecipeDiets)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.CreatedByUserId == null);

            if (recipe == null)
                return ServiceResult<AdminRecipeEditDto>.Failure("Oficjalny przepis nie istnieje.");

            var newInstructions = FormatInstructions(dto.Steps);
            if (recipe.Title != dto.Title.Trim() || recipe.Instructions != newInstructions)
            {
                _context.RecipeTranslations.RemoveRange(recipe.Translations);
            }

            recipe.ImageUrl = await _fileStorageService.ProcessImageAsync(dto.ImageUrl, recipe.ImageUrl, "recipes");
            ApplyContent(recipe, dto);

            _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
            _context.RecipeIngredients.AddRange(BuildIngredients(recipe.Id, dto));

            _context.RecipeDiets.RemoveRange(recipe.RecipeDiets);
            if (dto.DietIds != null && dto.DietIds.Any())
            {
                var newDiets = dto.DietIds.Distinct().Select(dietId => new RecipeDiet
                {
                    RecipeId = recipe.Id,
                    DietId = dietId
                });
                _context.RecipeDiets.AddRange(newDiets);
            }

            await _context.SaveChangesAsync();

            return await GetForEditAsync(recipe.Id);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid recipeId)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.Translations)
                .Include(r => r.RecipeDiets)
                .FirstOrDefaultAsync(r => r.Id == recipeId && r.CreatedByUserId == null);

            if (recipe == null)
                return ServiceResult<bool>.Failure("Oficjalny przepis nie istnieje.");

            if (!string.IsNullOrWhiteSpace(recipe.ImageUrl))
                _fileStorageService.DeleteFile(recipe.ImageUrl);

            var interactions = await _context.UserRecipeInteractions.Where(x => x.RecipeId == recipeId).ToListAsync();
            _context.UserRecipeInteractions.RemoveRange(interactions);
            _context.RecipeTranslations.RemoveRange(recipe.Translations);
            _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
            _context.RecipeDiets.RemoveRange(recipe.RecipeDiets);
            _context.Recipes.Remove(recipe);

            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }

        #region Helpers

        private async Task<string?> ValidateAsync(AdminRecipeEditDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return "Nazwa przepisu jest wymagana.";

            if (!dto.Steps.Any(s => !string.IsNullOrWhiteSpace(s)))
                return "Przepis musi zawierać co najmniej jeden krok przygotowania.";

            if (dto.Ingredients.Count == 0)
                return "Przepis musi zawierać co najmniej jeden składnik.";

            var productIds = dto.Ingredients.Select(i => i.ProductId).Distinct().ToList();
            if (productIds.Count != dto.Ingredients.Count)
                return "Ten sam składnik został dodany więcej niż raz.";

            var existingCount = await _context.Products.CountAsync(p => productIds.Contains(p.Id));
            if (existingCount != productIds.Count)
                return "Jeden ze składników nie istnieje w katalogu produktów.";

            if (dto.DietIds != null && dto.DietIds.Any())
            {
                var distinctDietIds = dto.DietIds.Distinct().ToList();
                var existingDietsCount = await _context.Diets.CountAsync(d => distinctDietIds.Contains(d.Id));
                if (existingDietsCount != distinctDietIds.Count)
                    return "Jedna z wybranych diet nie istnieje w słowniku.";
            }

            return null;
        }

        private static void ApplyContent(Recipe recipe, AdminRecipeEditDto dto)
        {
            recipe.Title = dto.Title.Trim();
            recipe.Instructions = FormatInstructions(dto.Steps);
            recipe.Servings = dto.Servings > 0 ? dto.Servings : 1;
            recipe.PrepTimeMinutes = dto.PrepTimeMinutes > 0 ? dto.PrepTimeMinutes : 15;
            recipe.Calories = dto.Calories;
        }

        private static string FormatInstructions(IEnumerable<string> steps) =>
            string.Join("\n", steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));

        private static IEnumerable<RecipeIngredient> BuildIngredients(Guid recipeId, AdminRecipeEditDto dto) =>
            dto.Ingredients.Select(i => new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Unit = i.Unit.Trim(),
                IsOptional = i.IsOptional
            });

        private static List<string> SplitSteps(string instructions) =>
            instructions
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Regex.Replace(s, @"^\d+[\.\)]\s*", string.Empty).Trim())
                .Where(s => s.Length > 0)
                .ToList();

        #endregion
    }
}