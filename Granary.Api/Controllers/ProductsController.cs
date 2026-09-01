using Granary.Api.Models.Dto;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers
{
    [ApiController]
    [Authorize(Roles = "User")]
    [Route("api/products")]
    [Produces("application/json")]
    public class ProductsController : GranaryBaseController
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>
        /// Pobiera listę wszystkich produktów.
        /// </summary>
        /// <remarks>
        /// Metoda dostępna publicznie. Zwraca pełną listę produktów zarejestrowanych w systemie.
        /// </remarks>
        /// <returns>Lista obiektów reprezentujących produkty.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _productService.GetAllAsync();
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera szczegółowe informacje o wybranym produkcie.
        /// </summary>
        /// <param name="id">Identyfikator produktu (np. GUID lub int).</param>
        /// <returns>Dane wybranego produktu lub błąd 404 w przypadku braku.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _productService.GetByIdAsync(id);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Tworzy nowy produkt w systemie.
        /// </summary>
        /// <remarks>
        /// Wymaga zalogowanego użytkownika. Oczekuje danych nowego produktu.
        /// </remarks>
        /// <param name="createProductDto">Model zawierający dane tworzonego produktu (np. nazwa, cena, opis).</param>
        /// <returns>Utworzony produkt wraz z przypisanym identyfikatorem.</returns>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] CreateOrUpdateProductDto createProductDto)
        {
            var result = await _productService.CreateAsync(createProductDto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Aktualizuje dane istniejącego produktu.
        /// </summary>
        /// <param name="id">Identyfikator aktualizowanego produktu.</param>
        /// <param name="updateProductDto">Obiekt ze zaktualizowanymi danymi produktu.</param>
        /// <returns>Zaktualizowane dane produktu.</returns>
        [HttpPut("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateOrUpdateProductDto updateProductDto)
        {
            var result = await _productService.UpdateAsync(id, updateProductDto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Usuwa produkt z bazy danych.
        /// </summary>
        /// <remarks>
        /// Wymaga uprawnień Administratora.
        /// </remarks>
        /// <param name="id">Identyfikator produktu do usunięcia.</param>
        /// <returns>Status operacji usunięcia (np. 204 No Content lub 200 OK).</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _productService.DeleteAsync(id);
            return HandleServiceResult(result);
        }
    }
}