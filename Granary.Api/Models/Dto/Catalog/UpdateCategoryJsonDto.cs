namespace Granary.Api.Models.Dto.Catalog
{
    /// <summary>
    /// Dane kategorii przesyłane jako JSON.
    /// </summary>
    public record UpdateCategoryJsonDto(
        string Name,
        int DisplayOrder,
        string? ImageUrl,
        bool IsPantryStaple = false
    );
}
