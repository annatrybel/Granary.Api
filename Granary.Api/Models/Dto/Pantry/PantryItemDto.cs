using System.ComponentModel.DataAnnotations;
using Granary.Api.Models.Enums;

namespace Granary.Api.Models.Dto
{
    public class PantryItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Emoji { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public StorageLocation StorageLocation { get; set; }
        public PantryItemStatus Status { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public DateOnly PurchaseDate { get; set; }
        public DateOnly ExpirationDate { get; set; }
    }
}