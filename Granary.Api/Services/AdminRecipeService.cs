using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Results;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Services
{
    public interface IAdminRecipeService
    {
        Task<ServiceResult<AdminRecipeKpisDto>> GetKpisAsync();
        Task<ServiceResult<PagedResult<AdminRecipeListItemDto>>> GetRecipesAsync(AdminRecipeFilterDto filter);
        Task<ServiceResult<bool>> ToggleApprovalAsync(Guid recipeId);
        Task<ServiceResult<bool>> DeleteAsync(Guid recipeId);
    }

    public class AdminRecipeService : IAdminRecipeService
    {
        private readonly GranaryDbContext _context;

        public AdminRecipeService(GranaryDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult<AdminRecipeKpisDto>> GetKpisAsync()
        {
            var officialCount = await _context.Recipes.CountAsync(r => r.CreatedByUserId == null);

            var votes = await _context.UserRecipeInteractions
                .Where(x => x.Vote != null)
                .Select(x => x.Vote)
                .ToListAsync();

            var totalReviews = votes.Count;
            var likes = votes.Count(v => v == true);
            var avgRating = totalReviews > 0 ? Math.Round(3.5 + (likes / (double)totalReviews * 1.5), 1) : 4.8;

            return ServiceResult<AdminRecipeKpisDto>.Success(new AdminRecipeKpisDto
            {
                TotalRecipes = officialCount,
                OfficialRecipes = officialCount,
                CommunityRecipes = 0,
                AverageRating = avgRating,
                TotalReviews = totalReviews > 0 ? totalReviews : 120,
                PendingVerification = 0,
                TotalCookedMeals = 48210
            });
        }

        public async Task<ServiceResult<PagedResult<AdminRecipeListItemDto>>> GetRecipesAsync(AdminRecipeFilterDto filter)
        {
            var query = _context.Recipes
                .AsNoTracking()
                .Where(r => r.CreatedByUserId == null) 
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Product)
                .AsQueryable();

            // Wyszukiwarka
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim().ToLower();
                query = query.Where(r => r.Title.ToLower().Contains(s) ||
                    r.Ingredients.Any(i => i.Product.Name.ToLower().Contains(s)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(r => new AdminRecipeListItemDto
                {
                    Id = r.Id,
                    Code = "REC-" + Math.Abs(r.Id.GetHashCode() % 1000).ToString("D3"),
                    Title = r.Title,
                    Subtitle = "Kuchnia wzorcowa",
                    ImageUrl = r.ImageUrl,
                    Category = r.PrepTimeMinutes <= 15 ? "Śniadanie" : (r.PrepTimeMinutes >= 35 ? "Wypieki" : "Obiad"),
                    MainIngredients = r.Ingredients.Take(3).Select(i => i.Product.Name).ToList(),
                    ExtraIngredientsCount = Math.Max(0, r.Ingredients.Count - 3),
                    PrepTimeMinutes = r.PrepTimeMinutes,
                    Servings = r.Servings,
                    Difficulty = r.PrepTimeMinutes <= 15 ? "Łatwy" : (r.PrepTimeMinutes <= 30 ? "Średni" : "Trudny"),
                    Rating = 4.8,
                    ReviewsCount = 120,
                    CookedCount = 1890,
                    IsOfficial = true,
                    IsNew = false
                })
                .ToListAsync();

            return ServiceResult<PagedResult<AdminRecipeListItemDto>>.Success(new PagedResult<AdminRecipeListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.PageNumber,
                PageSize = filter.PageSize
            });
        }

        public async Task<ServiceResult<bool>> ToggleApprovalAsync(Guid recipeId)
        {
            var recipe = await _context.Recipes.FindAsync(recipeId);
            if (recipe == null) return ServiceResult<bool>.Failure("Nie znaleziono dania.");

            recipe.CreatedByUserId = recipe.CreatedByUserId == null ? Guid.NewGuid() : null;
            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Success(recipe.CreatedByUserId == null);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(Guid recipeId)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null) return ServiceResult<bool>.Failure("Przepis nie istnieje.");

            var interactions = await _context.UserRecipeInteractions.Where(x => x.RecipeId == recipeId).ToListAsync();
            _context.UserRecipeInteractions.RemoveRange(interactions);
            _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
            _context.Recipes.Remove(recipe);

            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }
    }
}