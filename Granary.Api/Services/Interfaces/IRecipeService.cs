using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IRecipeService
    {
        Task<ServiceResult<IEnumerable<RecipeMatchDto>>> GetSuggestedRecipesAsync(Guid userId, IEnumerable<Guid>? selectedProductIds = null);
        Task<ServiceResult<RecipeDetailDto>> GetByIdAsync(Guid recipeId, Guid userId);
    }
}


