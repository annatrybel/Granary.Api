using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Granary.Api.Models.Enums;

namespace Granary.Api.Models.DatabaseModels
{
    [Table("PantryItems")] 
    public class PantryItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        [Required]
        public Guid ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; } = null!;

        [Column(TypeName = "decimal(10,2)")] 
        public decimal Quantity { get; set; } = 1;

        [Required, MaxLength(20)]
        public string Unit { get; set; } = "szt.";

        public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateOnly ExpirationDate { get; set; }

        public StorageLocation StorageLocation { get; set; } = StorageLocation.Pantry;
        public PantryItemStatus Status { get; set; } = PantryItemStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
