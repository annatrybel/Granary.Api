using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Translations;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranslationsController : GranaryBaseController
    {
        private readonly GranaryDbContext _context;
        private readonly IAiTranslationService _aiService;

        public TranslationsController(GranaryDbContext context, IAiTranslationService aiService)
        {
            _context = context;
            _aiService = aiService;
        }

        /// <summary>
        /// Pobiera słownik UI dla wskazanego języka (np. "en", "pl").
        /// </summary>
        [HttpGet("{langCode}")]
        public async Task<IActionResult> GetTranslations(string langCode)
        {
            var normalizedCode = (langCode ?? "pl").Trim().ToLower();

            var targetLanguage = await _context.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Code.ToLower() == normalizedCode);

            if (targetLanguage == null)
            {
                return Ok(new Dictionary<string, string>());
            }

            var dict = await _context.UiTranslations
                .AsNoTracking()
                .Where(t => t.LanguageId == targetLanguage.Id)
                .ToDictionaryAsync(t => t.Key, t => t.Value);

            return Ok(dict);
        }

        /// <summary>
        /// Endpoint Administratora: synchronizuje wszystkie brakujące tłumaczenia przez Groq AI.
        /// </summary>
        [Authorize(Roles = "Administrator")]
        [HttpPost("sync-ai")]
        public async Task<IActionResult> SyncTranslations()
        {
            // Jedno jasne wywołanie tłumaczące wszystko (UI + Produkty + Przepisy)
            var (totalCount, message) = await _aiService.SyncAllAsync();

            return Ok(new
            {
                success = true,
                count = totalCount,
                message = message
            });
        }


        public class SyncTranslationsPayloadDto
        {
            public List<TranslationKeyItemDto> Keys { get; set; } = new();
            public bool PruneStale { get; set; } = true;
        }

        public class TranslationKeyItemDto
        {
            public string Key { get; set; } = string.Empty;
            public string DefaultValue { get; set; } = string.Empty;
        }

        [HttpPost("sync-catalog")]
        [AllowAnonymous] // Na produkcji możesz zabezpieczyć nagłówkiem X-Deploy-Secret
        public async Task<IActionResult> SyncCatalogKeys([FromBody] SyncTranslationsPayloadDto payload)
        {
            var defaultLang = await _context.Languages
                .FirstOrDefaultAsync(l => l.IsDefault || l.Code == "pl");

            if (defaultLang == null)
            {
                return BadRequest("Brak zdefiniowanego języka domyślnego w tabeli Languages.");
            }

            var existingTranslations = await _context.UiTranslations
                .Where(t => t.LanguageId == defaultLang.Id)
                .ToDictionaryAsync(t => t.Key, t => t);

            int added = 0;
            int updated = 0;
            int pruned = 0;

            var incomingKeys = new HashSet<string>();

            foreach (var item in payload.Keys.Where(k => !string.IsNullOrWhiteSpace(k.Key)))
            {
                var cleanKey = item.Key.Trim();
                var cleanValue = item.DefaultValue.Trim();
                incomingKeys.Add(cleanKey);

                if (existingTranslations.TryGetValue(cleanKey, out var existing))
                {
                    if (existing.Value != cleanValue)
                    {
                        existing.Value = cleanValue;

                        var foreignTranslations = await _context.UiTranslations
                            .Where(t => t.Key == cleanKey && t.LanguageId != defaultLang.Id)
                            .ToListAsync();

                        _context.UiTranslations.RemoveRange(foreignTranslations);
                        updated++;
                    }
                }
                else
                {
                    _context.UiTranslations.Add(new UiTranslation
                    {
                        Id = Guid.NewGuid(),
                        LanguageId = defaultLang.Id,
                        Key = cleanKey,
                        Value = cleanValue
                    });
                    added++;
                }
            }

            if (payload.PruneStale)
            {
                var staleKeys = existingTranslations.Keys
                    .Where(k => !incomingKeys.Contains(k))
                    .ToList();

                if (staleKeys.Any())
                {
                    var obsoleteRecords = await _context.UiTranslations
                        .Where(t => staleKeys.Contains(t.Key))
                        .ToListAsync();

                    _context.UiTranslations.RemoveRange(obsoleteRecords);
                    pruned = staleKeys.Count;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { added, updated, pruned });
        }
    }
}