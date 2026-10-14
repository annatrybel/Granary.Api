using Microsoft.AspNetCore.Http;

namespace Granary.Api.Services.Storage
{
    public interface IFileStorageService
    {
        Task<string?> SaveImageFromUrlAsync(string imageUrl, string subfolder = "recipes");
        Task<string?> SaveFileAsync(IFormFile file, string subfolder = "avatars"); 
        void DeleteFile(string? relativePath);
    }
}