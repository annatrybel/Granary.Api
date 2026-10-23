namespace Granary.Api.Services.Storage
{
    public interface IFileStorageService
    {
        Task<string?> SaveFileAsync(IFormFile file, string subfolder = "avatars");
        Task<string?> SaveImageFromUrlAsync(string imageUrl, string subfolder = "recipes");
        Task<string?> SaveBase64ImageAsync(string base64Data, string subfolder = "products");
        void DeleteFile(string? storagePathOrUrl);
        Task<string?> ProcessImageAsync(string? newImageUrl, string? oldImageUrl = null, string subfolder = "images");
    }
}