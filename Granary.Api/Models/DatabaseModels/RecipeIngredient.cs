using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels
{
    public class RecipeIngredient
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal Quantity { get; set; }

        [Required, MaxLength(20)]
        public string Unit { get; set; } = "pcs";

        public bool IsOptional { get; set; } = false;
    }
}
