namespace Granary.Api.Models.Dto.Catalog;

public class ScannedProductDto
{
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = "Inne";
    public string DefaultUnit { get; set; } = "szt.";
    public int EstimatedExpiryDays { get; set; } = 7;
    public string SuggestedLocation { get; set; } = "Lodówka"; 
    public string? ImageUrl { get; set; }
}