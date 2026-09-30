using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IRecipeService
    {
        Task<ServiceResult<IEnumerable<RecipeMatchDto>>> GetSuggestedRecipesAsync(Guid userId, IEnumerable<Guid>? selectedProductIds = null);
        Task<ServiceResult<RecipeDetailDto>> GetByIdAsync(Guid recipeId, Guid userId);
        Task<ServiceResult<bool>> ToggleFavoriteAsync(Guid recipeId, Guid userId);
        Task<ServiceResult<VoteResultDto>> VoteRecipeAsync(Guid recipeId, Guid userId, bool isLike);
        Task<ServiceResult<IEnumerable<RecipeMatchDto>>> GetFavoritesAsync(Guid userId);
    }
}
