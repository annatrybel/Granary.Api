using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Granary.Api.Models.DatabaseModels
{
    [Table("ShoppingLists")]
    public class ShoppingList
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty; 

        public bool IsMain { get; set; } = false; 

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<ShoppingListItem> Items { get; set; } = new List<ShoppingListItem>();
    }
}