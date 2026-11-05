using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.AdminPanel
{
    /// <summary>
    /// Zarządza kategoriami produktów w panelu administratora.
    /// </summary>
    [ApiController]
    [Authorize(Roles = "Administrator")]
    [Route("api/admin/categories")]
    [Produces("application/json")]
    public class AdminCategoriesController : GranaryBaseController
    {
        private readonly IAdminCategoryService _categoryService;

        public AdminCategoriesController(IAdminCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// Pobiera wszystkie kategorie posortowane według kolejności wyświetlania.
        /// </summary>
        /// <returns>Lista kategorii wraz z adresami grafik.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAll()
        {
            return HandleServiceResult(await _categoryService.GetAllForAdminAsync());
        }

        /// <summary>
        /// Aktualizuje nazwę, kolejność i obraz kategorii.
        /// </summary>
        /// <remarks>
        /// Dla JSON pole ImageUrl przyjmuje bezpośredni adres obrazu. Ten sam adres obsługuje
        /// również multipart/form-data z polami Name, DisplayOrder, ImageUrl i ImageFile.
        /// </remarks>
        /// <param name="id">Identyfikator kategorii.</param>
        /// <param name="dto">Dane kategorii przesłane jako JSON.</param>
        /// <returns>Zaktualizowana kategoria.</returns>
        [HttpPut("{id:guid}")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateJson(Guid id, [FromBody] UpdateCategoryJsonDto dto)
        {
            return HandleServiceResult(await _categoryService.UpdateAsync(
                id,
                new UpdateCategoryDto(dto.Name, dto.DisplayOrder, dto.ImageUrl, null)));
        }

        /// <summary>
        /// Aktualizuje nazwę, kolejność i obraz kategorii przesłany jako plik.
        /// </summary>
        /// <param name="id">Identyfikator kategorii.</param>
        /// <param name="dto">Dane kategorii i opcjonalny plik obrazu.</param>
        /// <returns>Zaktualizowana kategoria.</returns>
        [HttpPut("{id:guid}")]
        [Consumes("multipart/form-data")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateForm(Guid id, [FromForm] UpdateCategoryDto dto)
        {
            return HandleServiceResult(await _categoryService.UpdateAsync(id, dto));
        }
    }
}
