using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granary.Api.Services.Storage
{
    public class FileStorageService : IFileStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly FileStorageSettings _settings;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(
            HttpClient httpClient,
            IOptions<FileStorageSettings> options,
            ILogger<FileStorageService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;
        }

        // 1. Zapis pliku przesłanego jako IFormFile (np. awatar)
        public async Task<string?> SaveFileAsync(IFormFile file, string subfolder = "avatars")
        {
            if (file == null || file.Length == 0) return null;

            try
            {
                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";

                var fileName = $"{Guid.NewGuid():N}{extension}";
                var storagePath = $"{subfolder}/{fileName}";

                await using var stream = file.OpenReadStream();
                return await UploadToStorageAsync(stream, storagePath, file.ContentType ?? "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisu pliku {FileName} do Supabase Storage", file.FileName);
                return null;
            }
        }

        // 2. Pobranie i zapis obrazka z zewnętrznego linku (np. wygenerowanego przez AI Pollinations)
        public async Task<string?> SaveImageFromUrlAsync(string imageUrl, string subfolder = "recipes")
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                request.Headers.Add("User-Agent", "GranaryApp/1.0 (Mozilla/5.0 compatible)");

                var downloadResponse = await _httpClient.SendAsync(request);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nie udało się pobrać grafiki z {Url}. Status: {Status}", imageUrl, downloadResponse.StatusCode);
                    return null;
                }

                var contentType = downloadResponse.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var extension = contentType.Contains("png") ? ".png" : (contentType.Contains("webp") ? ".webp" : ".jpg");
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var storagePath = $"{subfolder}/{fileName}";

                await using var stream = await downloadResponse.Content.ReadAsStreamAsync();
                return await UploadToStorageAsync(stream, storagePath, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania i zapisu obrazu z URL {Url} do Supabase", imageUrl);
                return null;
            }
        }

        // 3. Dekodowanie i zapis obrazka z formatu Base64 (np. z aparatu/galerii telefonu)
        public async Task<string?> SaveBase64ImageAsync(string base64Data, string subfolder = "products")
        {
            if (string.IsNullOrWhiteSpace(base64Data)) return null;

            try
            {
                string rawBase64 = base64Data;
                string extension = ".jpg";
                string contentType = "image/jpeg";

                var commaIndex = base64Data.IndexOf(',');
                if (commaIndex != -1)
                {
                    var header = base64Data.Substring(0, commaIndex);
                    rawBase64 = base64Data.Substring(commaIndex + 1);

                    if (header.Contains("image/png", StringComparison.OrdinalIgnoreCase))
                    {
                        extension = ".png";
                        contentType = "image/png";
                    }
                    else if (header.Contains("image/webp", StringComparison.OrdinalIgnoreCase))
                    {
                        extension = ".webp";
                        contentType = "image/webp";
                    }
                }

                var bytes = Convert.FromBase64String(rawBase64);
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var storagePath = $"{subfolder}/{fileName}";

                await using var stream = new MemoryStream(bytes);
                return await UploadToStorageAsync(stream, storagePath, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas dekodowania i zapisu obrazu Base64 do Supabase");
                return null;
            }
        }

        // 4. Usuwanie pliku z Supabase Storage (w tle)
        public void DeleteFile(string? storagePathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(storagePathOrUrl)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var storagePath = ExtractStoragePath(storagePathOrUrl);
                    if (string.IsNullOrWhiteSpace(storagePath)) return;

                    var baseUrl = _settings.Url.TrimEnd('/');
                    var deleteEndpoint = $"{baseUrl}/storage/v1/object/{_settings.Bucket}";

                    using var request = new HttpRequestMessage(HttpMethod.Delete, deleteEndpoint);
                    request.Headers.Add("apikey", _settings.Key);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Key);

                    var payload = new { prefixes = new[] { storagePath } };
                    request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("Błąd usuwania pliku {Path} z Supabase: {Status} - {Error}", storagePath, response.StatusCode, err);
                    }
                    else
                    {
                        _logger.LogInformation("Pomyślnie usunięto plik {Path} z Supabase Storage", storagePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wyjątek podczas usuwania pliku {Key} z Supabase Storage", storagePathOrUrl);
                }
            });
        }

        private async Task<string?> UploadToStorageAsync(Stream stream, string storagePath, string contentType)
        {
            var baseUrl = _settings.Url.TrimEnd('/');
            var uploadUrl = $"{baseUrl}/storage/v1/object/{_settings.Bucket}/{storagePath}";

            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            request.Headers.Add("apikey", _settings.Key);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Key);
            request.Headers.Add("x-upsert", "true");

            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Błąd uploadu do Supabase ({StatusCode}): {Body}", response.StatusCode, errorBody);
                return null;
            }

            return $"{baseUrl}/storage/v1/object/public/{_settings.Bucket}/{storagePath}";
        }

        private string ExtractStoragePath(string fileUrlOrKey)
        {
            var clean = fileUrlOrKey.Trim();
            var marker = $"/object/public/{_settings.Bucket}/";
            var index = clean.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (index >= 0)
            {
                return clean.Substring(index + marker.Length);
            }

            return clean.TrimStart('/');
        }


        public async Task<string?> ProcessImageAsync(string? newImageUrl, string? oldImageUrl = null, string subfolder = "images")
        {
            // 1. Jeśli użytkownik usunął zdjęcie (przekazał null lub pusty string):
            if (string.IsNullOrWhiteSpace(newImageUrl))
            {
                if (!string.IsNullOrWhiteSpace(oldImageUrl))
                {
                    DeleteFile(oldImageUrl);
                }
                return null;
            }

            // 2. Jeśli zdjęcie się nie zmieniło (jest takie samo jak stare):
            if (newImageUrl == oldImageUrl)
            {
                return oldImageUrl;
            }

            string? persistentUrl = null;

            // 3. Przypadek A: Zdjęcie z telefonu / aparatu w Base64
            if (newImageUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                persistentUrl = await SaveBase64ImageAsync(newImageUrl, subfolder);
            }
            // 4. Przypadek B: Nowe zdjęcie z AI (Pollinations)
            else if (newImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !newImageUrl.Contains("supabase.co"))
            {
                persistentUrl = await SaveImageFromUrlAsync(newImageUrl, subfolder);
            }
            else
            {
                // Zdjęcie już jest w Supabase
                return newImageUrl;
            }

            // Jeśli pomyślnie zapisano nowe zdjęcie w Supabase, usuwamy stare:
            if (!string.IsNullOrEmpty(persistentUrl) && !string.IsNullOrWhiteSpace(oldImageUrl))
            {
                DeleteFile(oldImageUrl);
            }

            return persistentUrl ?? newImageUrl;
        }
    }
}