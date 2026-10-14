using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Granary.Api.Services.Storage
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<LocalFileStorageService> _logger;

        public LocalFileStorageService(
            IWebHostEnvironment environment,
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            ILogger<LocalFileStorageService> logger)
        {
            _environment = environment;
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<string?> SaveFileAsync(IFormFile file, string subfolder = "avatars")
        {
            if (file == null || file.Length == 0) return null;

            try
            {
                var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var targetDir = Path.Combine(webRoot, "uploads", subfolder);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".jpg";
                }

                var fileName = $"{Guid.NewGuid():N}{extension}";
                var physicalPath = Path.Combine(targetDir, fileName);

                await using var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write);
                await file.CopyToAsync(fileStream);

                _logger.LogInformation("Zapisano przesłany plik w {Path}", physicalPath);

                var request = _httpContextAccessor.HttpContext?.Request;
                if (request != null)
                {
                    var baseUrl = $"{request.Scheme}://{request.Host}";
                    return $"{baseUrl}/uploads/{subfolder}/{fileName}";
                }

                return $"/uploads/{subfolder}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania przesłanego pliku {FileName}", file.FileName);
                return null;
            }
        }

        public async Task<string?> SaveImageFromUrlAsync(string imageUrl, string subfolder = "recipes")
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            try
            {
                var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var targetDir = Path.Combine(webRoot, "uploads", subfolder);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var fileName = $"{Guid.NewGuid():N}.jpg";
                var physicalPath = Path.Combine(targetDir, fileName);

                var response = await _httpClient.GetAsync(imageUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nie udało się pobrać obrazka z adresu {Url}. Status: {Status}", imageUrl, response.StatusCode);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(fileStream);

                _logger.LogInformation("Zapisano obrazek w {Path}", physicalPath);

                var request = _httpContextAccessor.HttpContext?.Request;
                if (request != null)
                {
                    var baseUrl = $"{request.Scheme}://{request.Host}";
                    return $"{baseUrl}/uploads/{subfolder}/{fileName}";
                }

                return $"/uploads/{subfolder}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania obrazka z adresu: {Url}", imageUrl);
                return null;
            }
        }

        public void DeleteFile(string? relativeOrFullUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeOrFullUrl)) return;

            try
            {
                var uri = new Uri(relativeOrFullUrl, UriKind.RelativeOrAbsolute);
                var path = uri.IsAbsoluteUri ? uri.AbsolutePath : relativeOrFullUrl;

                var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var physicalPath = Path.Combine(webRoot, path.TrimStart('/'));

                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się usunąć pliku {Path}", relativeOrFullUrl);
            }
        }
    }
}