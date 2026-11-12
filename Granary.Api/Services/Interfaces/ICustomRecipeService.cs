using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface ICustomRecipeService
    {
        Task<ServiceResult<RecipeDetailDto>> CreateCustomRecipeAsync(CreateCustomRecipeDto dto);
        Task<ServiceResult<RecipeDetailDto>> UpdateCustomRecipeAsync(Guid recipeId, CreateCustomRecipeDto dto);
        Task<ServiceResult<bool>> DeleteRecipeAsync(Guid recipeId);
    }
}
