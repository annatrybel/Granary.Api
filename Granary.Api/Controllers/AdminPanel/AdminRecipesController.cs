using Granary.Api.Controllers;
using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Recipes;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.AdminPanel
{
    /// <summary>
    /// Kontroler panelu administracyjnego odpowiedzialny za zarządzanie oficjalnymi (systemowymi) przepisami Granary.
    /// </summary>
    [ApiController]
    [Authorize(Roles = "Administrator")]
    [Route("api/admin/recipes")]
    [Produces("application/json")]
    public class AdminRecipesController : GranaryBaseController
    {
        private readonly IAdminRecipeService _adminRecipeService;
        private readonly IRecipeService _recipeService;

        public AdminRecipesController(
            IAdminRecipeService adminRecipeService,
            IRecipeService recipeService) 
        {
            _adminRecipeService = adminRecipeService;
            _recipeService = recipeService;
        }

        /// <summary>
        /// Pobiera zbiorcze wskaźniki efektywności (KPI) bazy oficjalnych przepisów.
        /// </summary>
        /// <response code="200">Zwrócono wskaźniki KPI (oficjalne dania, zadowolenie, ulubione, wymagające uwagi).</response>
        /// <response code="401">Wymagana rola Administrator.</response>
        [HttpGet("kpis")]
        [ProducesResponseType(typeof(AdminRecipeKpisDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetKpis()
        {
            var result = await _adminRecipeService.GetKpisAsync();
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera paginowaną listę dań oficjalnych z możliwością filtrowania po diecie, frazie i alertach jakościowych.
        /// </summary>
        /// <param name="filter">Filtry zapytań (w tym parametr needsAttention zawężający listę do dań z hejtem).</param>
        /// <response code="200">Zwrócono stronę listy przepisów.</response>
        /// <response code="401">Brak uprawnień administratora.</response>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AdminRecipeListItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRecipes([FromQuery] AdminRecipeFilterDto filter, [FromQuery] string lang = "pl")
        {
            var result = await _adminRecipeService.GetRecipesAsync(filter, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera dane szczegółowe oficjalnego dania przygotowane do formularza edycji.
        /// </summary>
        /// <param name="id">Identyfikator GUID dania systemowego.</param>
        /// <response code="200">Pomyślnie pobrano danie do edycji.</response>
        /// <response code="404">Danie nie istnieje lub jest prywatnym daniem użytkownika.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AdminRecipeEditDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetForEdit([FromRoute] Guid id)
        {
            var result = await _adminRecipeService.GetForEditAsync(id);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Tworzy nowy oficjalny przepis systemowy w bazie platformy.
        /// </summary>
        /// <param name="dto">Dane nowego dania ze składnikami z oficjalnego katalogu.</param>
        /// <response code="200">Przepis został utworzony.</response>
        /// <response code="400">Błąd walidacji danych.</response>
        [HttpPost]
        [ProducesResponseType(typeof(AdminRecipeEditDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] AdminRecipeEditDto dto)
        {
            var result = await _adminRecipeService.CreateAsync(dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Aktualizuje oficjalny przepis systemowy i unieważnia stare tłumaczenia AI w przypadku zmian tekstu.
        /// </summary>
        /// <param name="id">GUID aktualizowanego dania.</param>
        /// <param name="dto">Zaktualizowane dane dania.</param>
        /// <response code="200">Przepis zaktualizowany pomyślnie.</response>
        /// <response code="400">Błąd walidacji.</response>
        /// <response code="404">Nie znaleziono dania systemowego.</response>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(AdminRecipeEditDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] AdminRecipeEditDto dto)
        {
            var result = await _adminRecipeService.UpdateAsync(id, dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Trwale usuwa oficjalny przepis systemowy wraz z mediami i powiązaniami.
        /// </summary>
        /// <param name="id">GUID usuwanego dania.</param>
        /// <response code="200">Przepis został usunięty.</response>
        /// <response code="404">Nie znaleziono dania systemowego.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var result = await _adminRecipeService.DeleteAsync(id);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera listę dostępnych diet do formularzy i filtrów w panelu administratora.
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
