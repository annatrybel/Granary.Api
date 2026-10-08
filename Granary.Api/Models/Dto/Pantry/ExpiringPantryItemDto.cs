using Granary.Api.Models.Enums;

namespace Granary.Api.Models.Dto.Pantry
{
    public class ExpiringPantryItemDto
    {
        public Guid Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryImageUrl { get; set; }
        public StorageLocation StorageLocation { get; set; }
        public DateOnly? ExpirationDate { get; set; }
        public int DaysLeft { get; set; }       
    }
}
