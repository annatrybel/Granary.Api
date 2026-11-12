using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Ai;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
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
        private readonly ICustomRecipeService _customRecipeService;

        public RecipesController(
            IRecipeService recipeService,
            ICustomRecipeService customRecipeService)
        {
            _recipeService = recipeService;
            _customRecipeService = customRecipeService;
        }

        /// <summary>
        /// Główny endpoint pobierania przepisów:
        /// Obsługuje stronicowanie (Infinite Scroll), filtry backendowe, wyszukiwanie oraz scope (all / favorites / custom).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<RecipeMatchDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRecipes([FromQuery] RecipeFilterQueryDto filter)
        {
            var result = await _recipeService.GetFilteredRecipesAsync(filter);
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
            var result = await _recipeService.GetByIdAsync(id, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Przełącza status ulubionego (dodaje lub usuwa serduszko).
        /// </summary>
        [HttpPost("{id:guid}/favorite")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> ToggleFavorite(Guid id)
        {
            var result = await _recipeService.ToggleFavoriteAsync(id);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Głosowanie na przepis (łapka w górę / w dół).
        /// </summary>
        [HttpPost("{id:guid}/vote")]
        [ProducesResponseType(typeof(VoteResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Vote(Guid id, [FromBody] VoteRecipeRequest request)
        {
            var result = await _recipeService.VoteRecipeAsync(id, request.IsLike);
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
            var result = await _customRecipeService.CreateCustomRecipeAsync(dto);
            return HandleServiceResult(result);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _customRecipeService.DeleteRecipeAsync(id);
            return HandleServiceResult(result);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(RecipeDetailDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateCustomRecipeDto dto)
        {
            var result = await _customRecipeService.UpdateCustomRecipeAsync(id, dto);
            return HandleServiceResult(result);
        }

        [HttpPost("{id:guid}/cook")]
        public async Task<IActionResult> Cook([FromRoute] Guid id, [FromQuery] string lang = "pl")
        {
            var result = await _recipeService.CookRecipeAsync(id, lang);
            return HandleServiceResult(result);
        }

        public record GenerateRecipeImageRequest(string Title);


        /// <summary>
        /// Generuje obraz potrawy przy użyciu sztucznej inteligencji.
        /// </summary>
        [HttpPost("generate-image")]
        [ProducesResponseType(typeof(GeneratedRecipeImageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GenerateRecipeImage(
            [FromBody] GenerateRecipeImageRequest request,
            [FromServices] IAiImageService imageService)
        {
            if (string.IsNullOrWhiteSpace(request?.Title))
            {
                return HandleServiceResult(ServiceResult<GeneratedRecipeImageDto>.Failure("Tytuł przepisu jest wymagany."));
            }

            var (isValid, imageUrl, errorMessage) = await imageService.GenerateRecipeImageAsync(request.Title);

            if (!isValid || string.IsNullOrWhiteSpace(imageUrl))
            {
                return HandleServiceResult(ServiceResult<GeneratedRecipeImageDto>.Failure(errorMessage ?? "Nie udało się wygenerować obrazu."));
            }

            return HandleServiceResult(ServiceResult<GeneratedRecipeImageDto>.Success(new GeneratedRecipeImageDto(imageUrl)));
        }

        /// <summary>
        /// Pobiera słownik diet dla aplikacji mobilnej/webowej użytkownika.
        /// </summary>
        [HttpGet("diets")]
        [ProducesResponseType(typeof(IEnumerable<DietDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDiets([FromQuery] string lang = "pl")
        {
            var result = await _recipeService.GetDietsAsync(lang);
            return HandleServiceResult(result);
        }
    }
}