using Granary.Api.Models.Dto;
using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Services.Ai;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.Mobile
{
    /// <summary>
    /// Zarządzanie katalogiem produktów żywnościowych oraz rozpoznawaniem obrazów AI.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/catalog-products")]
    [Produces("application/json")]
    public class CatalogProductsController : GranaryBaseController
    {
        private readonly ICatalogProductService _catalogService;
        private readonly IAiVisionProductService _visionService;

        public CatalogProductsController(
            ICatalogProductService catalogService,
            IAiVisionProductService visionService)
        {
            _catalogService = catalogService;
            _visionService = visionService;
        }

        /// <summary>
        /// Wyszukuje produkty w katalogu ogólnym.
        /// </summary>
        /// <remarks>
        /// Przeszukuje produkty systemowe oraz własne produkty użytkownika.
        /// Obsługuje wielojęzyczność (tłumaczenia nazw) oraz możliwość wykluczenia produktów,
        /// które użytkownik posiada już w swojej aktywnej spiżarni.
        /// </remarks>
        /// <param name="query">Opcjonalna fraza wyszukiwania (nazwa produktu).</param>
        /// <param name="categoryId">Opcjonalny filtr identyfikatora kategorii.</param>
        /// <param name="excludePantry">Jeśli true, pomija produkty aktualnie znajdujące się w spiżarni użytkownika.</param>
        /// <param name="lang">Kod języka tłumaczenia (domyślnie "pl").</param>
        /// <returns>Lista dopasowanych produktów katalogowych (maksymalnie 50 wyników).</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CatalogProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Search(
            [FromQuery] string? query,
            [FromQuery] Guid? categoryId,
            [FromQuery] bool excludePantry = false,
            [FromQuery] string lang = "pl")
        {
            var result = await _catalogService.SearchAsync(query, categoryId, excludePantry, lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Rozpoznaje produkt spożywczy na podstawie przesłanego zdjęcia (Groq Vision AI).
        /// </summary>
        /// <remarks>
        /// Analizuje obraz przy użyciu modelu wizyjnego AI. Rozpoznaje nazwę produktu, sugerowaną kategorię,
        /// jednostkę miary oraz szacowany czas przydatności do spożycia.
        /// Zdjęcie powinno być skompresowane po stronie klienta (JPEG/PNG).
        /// </remarks>
        /// <param name="image">Plik ze zdjęciem produktu (przesyłany jako multipart/form-data).</param>
        /// <returns>Rozpoznane dane produktu wraz z przewidywanym terminem ważności.</returns>
        [HttpPost("recognize-image")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RecognizeImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest("Nie przesłano pliku ze zdjęciem.");
            }

            var result = await _visionService.RecognizeProductFromImageAsync(image);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera listę kategorii produktów w wybranym języku.
        /// </summary>
        /// <remarks>
        /// Zwraca kategorie posortowane według ustalonej kolejności wyświetlania (DisplayOrder).
        /// </remarks>
        /// <param name="lang">Kod języka tłumaczenia (domyślnie "pl").</param>
        /// <returns>Lista dostępnych kategorii produktów.</returns>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCategories([FromQuery] string lang = "pl")
        {
            var result = await _catalogService.GetCategoriesAsync(lang);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Tworzy nowy produkt niestandardowy lub zwraca istniejący o tej samej nazwie.
        /// </summary>
        /// <remarks>
        /// Pozwala użytkownikowi zdefiniować własny produkt, jeśli nie istnieje on w globalnym katalogu.
        /// Jeśli użytkownik stworzył już wcześniej produkt o identycznej nazwie, zostanie on zwrócony zamiast tworzenia duplikatu.
        /// </remarks>
        /// <param name="dto">Dane tworzonego produktu (wymagana unikalna nazwa, opcjonalna kategoria i jednostka).</param>
        /// <returns>Utworzony lub odnaleziony produkt katalogowy.</returns>
        [HttpPost("custom")]
        [ProducesResponseType(typeof(CatalogProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCustom([FromBody] CatalogProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Nazwa produktu jest wymagana.");
            }

            var productId = await _catalogService.GetOrCreateCustomProductAsync(
                dto.Name,
                dto.CategoryId != Guid.Empty ? dto.CategoryId : null,
                dto.DefaultUnit,
                dto.ImageUrl
            );

            return HandleServiceResult(await _catalogService.GetByIdAsync(productId));
        }
    }
}