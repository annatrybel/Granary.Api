using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.Mobile
{
    [ApiController]
    [Authorize]
    [Route("api/recipes")]
    [Produces("application/json")]
    public class RecipesController : GranaryBaseController
    {
        private readonly IRecipeService _recipeService;

        public RecipesController(IRecipeService recipeService)
        {
            _recipeService = recipeService;
        }

        /// <summary>
        /// Główny endpoint pobierania przepisów:
        /// Obsługuje stronicowanie (Infinite Scroll), filtry backendowe, wyszukiwanie oraz scope (all / favorites / custom).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<RecipeMatchDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRecipes([FromQuery] RecipeFilterQueryDto filter)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.GetFilteredRecipesAsync(userId, filter);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera pełne szczegóły wybranego przepisu (instrukcja krok po kroku, status składników ze spiżarni).
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(RecipeDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, [FromQuery] string lang = "pl")
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.GetByIdAsync(id, userId, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Przełącza status ulubionego (dodaje lub usuwa serduszko).
        /// </summary>
        [HttpPost("{id:guid}/favorite")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> ToggleFavorite(Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.ToggleFavoriteAsync(id, userId);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Głosowanie na przepis (łapka w górę / w dół).
        /// </summary>
        [HttpPost("{id:guid}/vote")]
        [ProducesResponseType(typeof(VoteResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Vote(Guid id, [FromBody] VoteRecipeRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.VoteRecipeAsync(id, userId, request.IsLike);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Tworzy nowy własny przepis powiązany z zalogowanym użytkownikiem.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(RecipeDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCustomRecipe([FromBody] CreateCustomRecipeDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.CreateCustomRecipeAsync(dto, userId);
            return HandleServiceResult(result);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.DeleteRecipeAsync(id, userId);
            return HandleServiceResult(result);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(RecipeDetailDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateCustomRecipeDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.UpdateCustomRecipeAsync(id, dto, userId);
            return HandleServiceResult(result);
        }



        public record GenerateRecipeImageRequest(string Title);


        [Authorize]
        [HttpPost("generate-image")]
        public async Task<IActionResult> GenerateRecipeImage(
            [FromBody] GenerateRecipeImageRequest request,
            [FromServices] IAiImageService imageService)
        {
            var (isValid, imageUrl, errorMessage) = await imageService.GenerateRecipeImageAsync(request.Title);

            if (!isValid) return BadRequest(new { success = false, message = errorMessage });
            return Ok(new { success = true, imageUrl });
        }
    }
}