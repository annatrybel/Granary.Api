using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels
{
    public class Product
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Barcode { get; set; }

        [MaxLength(10)]
        public string? Emoji { get; set; } 

        [Required, MaxLength(20)]
        public string DefaultUnit { get; set; } = "pcs";

        public int? DefaultShelfLifeDays { get; set; }

        public Guid? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }

        public ICollection<PantryItem> PantryItems { get; set; } = new List<PantryItem>();
        public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    }
}