using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Granary.Api.Models.DatabaseModels
{
    [Table("ProductTranslations")]
    public class ProductTranslation
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; } = null!;

        [Required]
        public Guid LanguageId { get; set; }

        [ForeignKey(nameof(LanguageId))]
        public Language Language { get; set; } = null!;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Unit { get; set; } 
    }
}