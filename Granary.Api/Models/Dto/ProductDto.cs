namespace Granary.Api.Models.Dto
{
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string DefaultUnit { get; set; } = "pcs";
        public int? DefaultShelfLifeDays { get; set; }
        public string? Emoji { get; set; }

        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}
