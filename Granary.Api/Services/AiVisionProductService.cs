using Granary.Api.Models.Dto.Catalog;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Granary.Api.Services;

public interface IAiVisionProductService
{
    Task<ServiceResult<ScannedProductDto>> RecognizeProductFromImageAsync(IFormFile imageFile);
}

public class AiVisionProductService : IAiVisionProductService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<AiVisionProductService> _logger;

    public AiVisionProductService(
        HttpClient httpClient,
        IConfiguration configuration,
        IFileStorageService fileStorageService,
        ILogger<AiVisionProductService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<ServiceResult<ScannedProductDto>> RecognizeProductFromImageAsync(IFormFile imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
            return ServiceResult<ScannedProductDto >.Failure("Brak przesłanego zdjęcia.");

        var apiKey = _configuration["GROQ_API_KEY"] ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return ServiceResult<ScannedProductDto>.Failure("Brak skonfigurowanego klucza GROQ_API_KEY.");

        try
        {
            var savedImageUrl = await _fileStorageService.SaveFileAsync(imageFile, "products");

            // 2. Przygotowujemy obraz Base64
            using var memoryStream = new MemoryStream();
            await imageFile.CopyToAsync(memoryStream);
            var base64Image = Convert.ToBase64String(memoryStream.ToArray());
            var mimeType = string.IsNullOrWhiteSpace(imageFile.ContentType) ? "image/jpeg" : imageFile.ContentType;

            var systemPrompt = @"Analyze this food product image. Identify what it is and suggest storage details.
Return STRICTLY a JSON object matching this schema, without any markdown:
{
  ""name"": ""Product name in Polish (e.g. Pomidory, Mleko 3.2%, Banany, Ser gouda)"",
  ""categoryName"": ""One of: Nabiał, Warzywa, Owoce, Mięso, Pieczywo, Napoje, Mrożonki, Inne"",
  ""suggestedLocation"": ""One of: Lodówka, Spiżarnia, Zamrażarka"",
  ""estimatedExpiryDays"": 7,
  ""defaultUnit"": ""szt.""
}";

            var requestBody = new
            {
                model = "qwen/qwen3.8-27b", 
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = systemPrompt },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:{mimeType};base64,{base64Image}" }
                            }
                        }
                    }
                },
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Groq Vision API zwróciło błąd: {Error}", errContent);
                return ServiceResult<ScannedProductDto>.Failure($"Groq API Error: {errContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var contentString = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(contentString))
                return ServiceResult<ScannedProductDto>.Failure("Pusta odpowiedź z modelu AI.");

            using var resultDoc = JsonDocument.Parse(contentString);
            var root = resultDoc.RootElement;

            return ServiceResult<ScannedProductDto>.Success(new ScannedProductDto
            {
                Name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "Produkt" : "Produkt",
                CategoryName = root.TryGetProperty("categoryName", out var c) ? c.GetString() ?? "Inne" : "Inne",
                SuggestedLocation = root.TryGetProperty("suggestedLocation", out var l) ? l.GetString() ?? "Lodówka" : "Lodówka",
                EstimatedExpiryDays = root.TryGetProperty("estimatedExpiryDays", out var d) ? d.GetInt32() : 7,
                DefaultUnit = root.TryGetProperty("defaultUnit", out var u) ? u.GetString() ?? "szt." : "szt.",
                ImageUrl = savedImageUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas analizy obrazu przez Groq.");
            return ServiceResult<ScannedProductDto>.Failure("Wystąpił błąd podczas analizy obrazu.");
        }
    }
}