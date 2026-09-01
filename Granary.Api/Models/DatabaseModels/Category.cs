using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels
{
    public class Category
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? IconSlug { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
