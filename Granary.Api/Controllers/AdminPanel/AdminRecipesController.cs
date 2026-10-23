using Granary.Api.Controllers;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.AdminPanel
{
    [ApiController]
    [Authorize(Roles = "Administrator")]
    [Route("api/admin/recipes")]
    [Produces("application/json")]
    public class AdminRecipesController : GranaryBaseController
    {
        private readonly IAdminRecipeService _recipeService;

        public AdminRecipesController(IAdminRecipeService recipeService)
        {
            _recipeService = recipeService;
        }

        [HttpGet("kpis")]
        [ProducesResponseType(typeof(AdminRecipeKpisDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetKpis()
        {
            var result = await _recipeService.GetKpisAsync();
            return HandleServiceResult(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AdminRecipeListItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRecipes([FromQuery] AdminRecipeFilterDto filter)
        {
            var result = await _recipeService.GetRecipesAsync(filter);
            return HandleServiceResult(result);
        }

        [HttpPost("{id}/toggle-approval")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> ToggleApproval([FromRoute] Guid id)
        {
            var result = await _recipeService.ToggleApprovalAsync(id);
            return HandleServiceResult(result);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var result = await _recipeService.DeleteAsync(id);
            return HandleServiceResult(result);
        }
    }
}