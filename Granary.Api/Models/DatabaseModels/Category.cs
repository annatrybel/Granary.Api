using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Granary.Api.Models.DatabaseModels
{
    [Table("Categories")] 
    public class Category
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
