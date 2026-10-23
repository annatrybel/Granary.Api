using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Services
{
    public class AiTranslationService : IAiTranslationService
    {
        private readonly GranaryDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<AiTranslationService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public AiTranslationService(
            GranaryDbContext context,
            HttpClient httpClient,
            IConfiguration config,
            ILogger<AiTranslationService> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<(int TotalCount, string Message)> SyncAllAsync()
        {
            var (uiCount, _) = await SyncUiTranslationsAsync();
            var (prodCount, _) = await SyncProductTranslationsAsync();
            var (recCount, _) = await SyncRecipeTranslationsAsync();

            int total = uiCount + prodCount + recCount;
            return (total, $"Synchronizacja zakończona: UI ({uiCount}), Produkty ({prodCount}), Przepisy ({recCount}). Łącznie: {total}.");
        }

        public async Task<(int Count, string Message)> SyncUiTranslationsAsync()
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) return (0, "Błąd: Brak klucza API Groq w konfiguracji.");

            var defaultLang = await GetDefaultLanguageAsync();
            if (defaultLang == null) return (0, "Błąd: Brak zdefiniowanego języka domyślnego.");

            var targetLanguages = await GetTargetLanguagesAsync(defaultLang.Id);
            if (!targetLanguages.Any()) return (0, "Brak aktywnych języków docelowych.");

            var baseTranslations = await _context.UiTranslations
                .Where(t => t.LanguageId == defaultLang.Id)
                .ToDictionaryAsync(t => t.Key, t => t.Value);

            if (!baseTranslations.Any()) return (0, "Brak bazowych fraz w tabeli UiTranslations.");

            int totalAdded = 0;

            foreach (var targetLang in targetLanguages)
            {
                var existingKeys = await _context.UiTranslations
                    .Where(t => t.LanguageId == targetLang.Id)
                    .Select(t => t.Key)
                    .ToHashSetAsync();

                var missing = baseTranslations
                    .Where(p => !existingKeys.Contains(p.Key))
                    .ToDictionary(p => p.Key, p => p.Value);

                if (!missing.Any()) continue;

                foreach (var chunk in missing.Chunk(25))
                {
                    var chunkDict = chunk.ToDictionary(k => k.Key, v => v.Value);

                    var systemPrompt = "You are a professional software localizer. Output only a raw valid JSON object. Keep keys unchanged. No markdown, no explanations.";
                    var userPrompt = $@"Translate the values of this JSON dictionary from Polish to {targetLang.Name} ({targetLang.Code}):
{JsonSerializer.Serialize(chunkDict, JsonOptions)}";

                    var translatedDict = await CallGroqJsonAsync<Dictionary<string, string>>(systemPrompt, userPrompt, apiKey);
                    if (translatedDict == null) continue;

                    foreach (var (key, val) in translatedDict)
                    {
                        if (string.IsNullOrWhiteSpace(val)) continue;

                        _context.UiTranslations.Add(new UiTranslation
                        {
                            Id = Guid.NewGuid(),
                            LanguageId = targetLang.Id,
                            Key = key,
                            Value = val.Trim()
                        });
                        totalAdded++;
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return (totalAdded, $"Przetłumaczono i zapisano {totalAdded} fraz UI.");
        }

        public async Task<(int Count, string Message)> SyncProductTranslationsAsync()
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) return (0, "Błąd: Brak klucza API Groq w konfiguracji.");

            var defaultLang = await GetDefaultLanguageAsync();
            var targetLanguages = await GetTargetLanguagesAsync(defaultLang?.Id ?? Guid.Empty);
            if (!targetLanguages.Any()) return (0, "Brak aktywnych języków docelowych.");

            int totalAdded = 0;

            foreach (var targetLang in targetLanguages)
            {
                var missingProducts = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.CreatedByUserId == null && !p.Translations.Any(t => t.LanguageId == targetLang.Id))
                    .Select(p => new { p.Id, p.Name })
                    .ToListAsync();

                if (!missingProducts.Any()) continue;

                foreach (var chunk in missingProducts.Chunk(35))
                {
                    var chunkDict = chunk.ToDictionary(p => p.Id.ToString(), p => p.Name);

                    var systemPrompt = "You are a culinary translator. Output only raw valid JSON object mapping input GUID keys to translated names. No markdown formatting, no explanations.";
                    var userPrompt = $@"Translate these grocery item names from Polish to {targetLang.Name} ({targetLang.Code}). Keep the GUID keys exactly as provided:
{JsonSerializer.Serialize(chunkDict, JsonOptions)}";

                    var translatedDict = await CallGroqJsonAsync<Dictionary<string, string>>(systemPrompt, userPrompt, apiKey);
                    if (translatedDict == null) continue;

                    foreach (var (guidStr, translatedName) in translatedDict)
                    {
                        if (Guid.TryParse(guidStr, out var productId) && !string.IsNullOrWhiteSpace(translatedName))
                        {
                            _context.ProductTranslations.Add(new ProductTranslation
                            {
                                Id = Guid.NewGuid(),
                                ProductId = productId,
                                LanguageId = targetLang.Id,
                                Name = translatedName.Trim()
                            });
                            totalAdded++;
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return (totalAdded, $"Przetłumaczono i zapisano {totalAdded} produktów.");
        }

        public async Task<(int Count, string Message)> SyncRecipeTranslationsAsync()
        {
            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) return (0, "Błąd: Brak klucza API Groq w konfiguracji.");

            var defaultLang = await GetDefaultLanguageAsync();
            var targetLanguages = await GetTargetLanguagesAsync(defaultLang?.Id ?? Guid.Empty);
            if (!targetLanguages.Any()) return (0, "Brak aktywnych języków docelowych.");

            int totalAdded = 0;

            foreach (var targetLang in targetLanguages)
            {
                var missingRecipes = await _context.Recipes
                    .AsNoTracking()
                    .Where(r => r.CreatedByUserId == null && !r.Translations.Any(t => t.LanguageId == targetLang.Id))
                    .Select(r => new
                    {
                        r.Id,
                        r.Title,
                        r.Instructions
                    })
                    .ToListAsync();

                if (!missingRecipes.Any()) continue;

                foreach (var chunk in missingRecipes.Chunk(3))
                {
                    var chunkDict = chunk.ToDictionary(
                        r => r.Id.ToString(),
                        r => new RecipeTranslationPayload
                        {
                            Title = r.Title,
                            Instructions = r.Instructions
                        });

                    var systemPrompt = "You are a professional culinary chef and translator. Output only raw valid JSON object. Keep the GUID keys and object structure exactly as provided. No markdown.";
                    var userPrompt = $@"Translate the Title, Description, and Instructions of these recipes from Polish to {targetLang.Name} ({targetLang.Code}):
{JsonSerializer.Serialize(chunkDict, JsonOptions)}";

                    var translatedDict = await CallGroqJsonAsync<Dictionary<string, RecipeTranslationPayload>>(systemPrompt, userPrompt, apiKey);
                    if (translatedDict == null) continue;

                    foreach (var (guidStr, payload) in translatedDict)
                    {
                        if (Guid.TryParse(guidStr, out var recipeId) && !string.IsNullOrWhiteSpace(payload.Title))
                        {
                            _context.RecipeTranslations.Add(new RecipeTranslation
                            {
                                Id = Guid.NewGuid(),
                                RecipeId = recipeId,
                                LanguageId = targetLang.Id,
                                Title = payload.Title.Trim(),
                                Instructions = payload.Instructions ?? string.Empty
                            });
                            totalAdded++;
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return (totalAdded, $"Przetłumaczono i zapisano {totalAdded} przepisów.");
        }


        private async Task<T?> CallGroqJsonAsync<T>(string systemPrompt, string userPrompt, string apiKey) where T : class
        {
            var model = _config["Groq:Model"] ?? "openai/gpt-oss-20b";

            var requestBody = new
            {
                model = model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.1,
                max_tokens = 4096
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Błąd API Groq ({StatusCode}): {Response}", response.StatusCode, responseContent);
                    return null;
                }

                using var doc = JsonDocument.Parse(responseContent);
                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    content = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }

                return JsonSerializer.Deserialize<T>(content, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas komunikacji z Groq lub deserializacji JSON.");
                return null;
            }
        }

        private string? GetApiKey()
        {
            var key = _config["Groq:ApiKey"]
                   ?? _config["GROQ_API_KEY"]
                   ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");

            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }

        private async Task<Language?> GetDefaultLanguageAsync()
        {
            return await _context.Languages
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.IsDefault)
                ?? await _context.Languages.AsNoTracking().FirstOrDefaultAsync(l => l.Code == "pl");
        }

        private async Task<List<Language>> GetTargetLanguagesAsync(Guid defaultLangId)
        {
            return await _context.Languages
                .AsNoTracking()
                .Where(l => l.IsActive && l.Id != defaultLangId)
                .ToListAsync();
        }

        private sealed class RecipeTranslationPayload
        {
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? Instructions { get; set; }
        }
    }
}