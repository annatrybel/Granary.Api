namespace Granary.Api.Services.Interfaces
{
    public interface IAiTranslationService
    {
        Task<(int Count, string Message)> SyncUiTranslationsAsync();
        Task<(int Count, string Message)> SyncProductTranslationsAsync();
        Task<(int Count, string Message)> SyncRecipeTranslationsAsync();
        Task<(int TotalCount, string Message)> SyncAllAsync();
    }
}