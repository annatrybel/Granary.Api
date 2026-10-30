using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Granary.Api.Models.DatabaseModels
{
    [Table("ShoppingListItems")]
    public class ShoppingListItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ShoppingListId { get; set; }

        [ForeignKey(nameof(ShoppingListId))]
        public ShoppingList ShoppingList { get; set; } = null!;

        public Guid? ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; } = 1;

        [MaxLength(20)]
        public string? Unit { get; set; } = "szt.";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsBought { get; set; }
    }
}