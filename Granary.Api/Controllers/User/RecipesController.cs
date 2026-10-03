using Granary.Api.Models.Dto.Recipes;
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
        /// Pobiera sugerowane przepisy posortowane wg dopasowania i wygasających produktów.
        /// </summary>
        [HttpGet("suggestions")]
        [ProducesResponseType(typeof(IEnumerable<RecipeMatchDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSuggestions([FromQuery] string? items)
        {
            var userId = GetCurrentUserId();

            List<Guid>? selectedIds = null;
            if (!string.IsNullOrWhiteSpace(items))
            {
                selectedIds = items
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToList();
            }

            var result = await _recipeService.GetSuggestedRecipesAsync(userId, selectedIds);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera przepisy użytkownika do sekcji "Twoje i ulubione".
        /// </summary>
        [HttpGet("favorites")]
        [ProducesResponseType(typeof(IEnumerable<RecipeMatchDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.GetFavoritesAsync(userId);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera pełne szczegóły wybranego przepisu z instrukcją, statystykami polubień i głosem użytkownika.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(RecipeDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.GetByIdAsync(id, userId);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Przełącza status serduszka (dodaje lub usuwa z ulubionych).
        /// </summary>
        [HttpPost("{id}/favorite")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> ToggleFavorite(Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.ToggleFavoriteAsync(id, userId);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Głosowanie na przepis .
        /// </summary>
        [HttpPost("{id}/vote")]
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
        /// <param name="dto">Dane nowego przepisu (tytuł, składniki, kroki przygotowania, zdjęcie).</param>
        /// <returns>Utworzony przepis ze szczegółami.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(RecipeDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCustomRecipe([FromBody] CreateCustomRecipeDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _recipeService.CreateCustomRecipeAsync(dto, userId);
            return HandleServiceResult(result);
        }
    }
}
