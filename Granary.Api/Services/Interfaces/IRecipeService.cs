using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IRecipeService
    {
        Task<ServiceResult<PagedResult<RecipeMatchDto>>> GetFilteredRecipesAsync(Guid userId, RecipeFilterQueryDto filter);
        Task<ServiceResult<RecipeDetailDto>> GetByIdAsync(Guid recipeId, Guid userId, string lang = "pl");
        Task<ServiceResult<bool>> ToggleFavoriteAsync(Guid recipeId, Guid userId);
        Task<ServiceResult<VoteResultDto>> VoteRecipeAsync(Guid recipeId, Guid userId, bool isLike);
        Task<ServiceResult<RecipeDetailDto>> CreateCustomRecipeAsync(CreateCustomRecipeDto dto, Guid userId);
        Task<ServiceResult<RecipeDetailDto>> UpdateCustomRecipeAsync(Guid recipeId, CreateCustomRecipeDto dto, Guid userId);
        Task<ServiceResult<bool>> DeleteRecipeAsync(Guid recipeId, Guid userId);
    }
}