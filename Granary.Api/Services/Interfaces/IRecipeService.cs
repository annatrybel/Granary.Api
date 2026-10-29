using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IRecipeService
    {
        Task<ServiceResult<PagedResult<RecipeMatchDto>>> GetFilteredRecipesAsync(RecipeFilterQueryDto filter);
        Task<ServiceResult<RecipeDetailDto>> GetByIdAsync(Guid recipeId, string lang = "pl");
        Task<ServiceResult<bool>> ToggleFavoriteAsync(Guid recipeId);
        Task<ServiceResult<VoteResultDto>> VoteRecipeAsync(Guid recipeId, bool isLike);
        Task<ServiceResult<RecipeDetailDto>> CreateCustomRecipeAsync(CreateCustomRecipeDto dto);
        Task<ServiceResult<RecipeDetailDto>> UpdateCustomRecipeAsync(Guid recipeId, CreateCustomRecipeDto dto);
        Task<ServiceResult<bool>> DeleteRecipeAsync(Guid recipeId);
    }
}