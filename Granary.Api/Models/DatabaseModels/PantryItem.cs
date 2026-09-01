using Granary.Api.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels
{
    public class PantryItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal Quantity { get; set; }

        [Required, MaxLength(20)]
        public string Unit { get; set; } = "pcs";

        public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateOnly ExpirationDate { get; set; }

        public StorageLocation StorageLocation { get; set; } = StorageLocation.Pantry;
        public PantryItemStatus Status { get; set; } = PantryItemStatus.Active;
    }
}
