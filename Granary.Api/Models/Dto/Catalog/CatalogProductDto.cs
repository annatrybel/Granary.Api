namespace Granary.Api.Models.Dto
{
    public class CatalogProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? ImageUrl { get; set; }
        public string? DefaultUnit { get; set; }
        public int DefaultShelfLifeDays { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public Guid? CreatedByUserId { get; set; } 
    }
}