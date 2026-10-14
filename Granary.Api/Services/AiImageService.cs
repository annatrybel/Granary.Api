using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Granary.Api.Services
{
    public interface IAiImageService
    {
        Task<(bool IsValid, string ImageUrl, string? ErrorMessage)> GenerateRecipeImageAsync(string recipeTitle);
        Task<(bool IsValid, string ImageUrl, string? ErrorMessage)> GenerateProductImageAsync(string productName);
    }

    public class AiImageService : IAiImageService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<AiImageService> _logger;

        public AiImageService(HttpClient httpClient, IConfiguration config, ILogger<AiImageService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public Task<(bool IsValid, string ImageUrl, string? ErrorMessage)> GenerateRecipeImageAsync(string recipeTitle)
        {
            var systemPrompt = @"
Jesteś moderatorem bezpieczeństwa i ekspertem od fotografii kulinarnej dla aplikacji Zero-Waste.
Oceń nazwę dania wpisanego przez użytkownika:
1. Jeśli zawiera wulgaryzmy, przemoc, NSFW lub PRZEDMIOTY NIEJADALNE/ŚMIECI:
   Zwróć: {""isValid"": false, ""prompt"": """"}
2. Jeśli to prawdziwa potrawa, obiad, zupa, deser lub napój:
   Stwórz wykwintny angielski prompt fotografii kulinarnej (Gourmet plated dish food photography).
   Skup się na: apetycznym platingu, ciepłym świetle studyjnym, estetyce magazynu kulinarnego, kadrze potrawy na stole.
   Zwróć: {""isValid"": true, ""prompt"": ""...twój angielski prompt...""}

Zwróć WYŁĄCZNIE czysty JSON.";

            return GenerateImageCoreAsync(recipeTitle, systemPrompt, width: 700, height: 450);
        }

        public Task<(bool IsValid, string ImageUrl, string? ErrorMessage)> GenerateProductImageAsync(string productName)
        {
            var systemPrompt = @"
Jesteś moderatorem bezpieczeństwa i ekspertem od fotografii produktowej e-commerce dla spiżarni.
Oceń nazwę artykułu spożywczego/składnika:
1. Jeśli zawiera wulgaryzmy, przemoc, NSFW lub PRZEDMIOTY NIEJADALNE/ŚMIECI:
   Zwróć: {""isValid"": false, ""prompt"": """"}
2. Jeśli to poprawny artykuł spożywczy, owoc, warzywo, nabiał, pieczywo lub składnik:
   Stwórz wyrazisty angielski prompt fotografii produktowej składnika (Commercial grocery studio shot).
   Skup się na: czystym neutralnym tle kuchennym, świeżości, naturalnych kolorach, pojedynczym przedmiocie w kadrze.
   Zwróć: {""isValid"": true, ""prompt"": ""...twój angielski prompt...""}

Zwróć WYŁĄCZNIE czysty JSON.";

            return GenerateImageCoreAsync(productName, systemPrompt, width: 500, height: 500);
        }

        private async Task<(bool IsValid, string ImageUrl, string? ErrorMessage)> GenerateImageCoreAsync(
            string itemName,
            string systemPrompt,
            int width,
            int height)
        {
            var apiKey = _config["Groq:ApiKey"]
                      ?? _config["GROQ_API_KEY"]
                      ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (false, string.Empty, "Brak skonfigurowanego klucza Groq.");
            }

            var safeApiKey = apiKey.Trim();

            var requestBody = new
            {
                model = "openai/gpt-oss-20b",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Nazwa: {itemName}" }
                },
                temperature = 0.1
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", safeApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Błąd Groq przy generowaniu promptu ({StatusCode}): {Error}", response.StatusCode, errorContent);
                return (false, string.Empty, "Błąd komunikacji z modelem AI.");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                content = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            using var resultDoc = JsonDocument.Parse(content);
            var isValid = resultDoc.RootElement.GetProperty("isValid").GetBoolean();

            if (!isValid)
            {
                return (false, string.Empty, "Podana nazwa nie jest artykułem spożywczym lub narusza zasady bezpieczeństwa.");
            }

            var promptText = resultDoc.RootElement.GetProperty("prompt").GetString() ?? itemName;

            var encodedPrompt = Uri.EscapeDataString(promptText);
            var negative = Uri.EscapeDataString("nsfw, ugly, dirty, human, face, text, logo, watermark, bad quality, messy");
            var seed = Random.Shared.Next(100000, 999999);

            var finalUrl = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={width}&height={height}&model=flux&seed={seed}&nologo=true&safe=true&negative={negative}";

            return (true, finalUrl, null);
        }
    }
}