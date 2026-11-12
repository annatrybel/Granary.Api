using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Recipes
{
    public class AdminRecipeIngredientDto
    {
        [Required]
        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        [Range(0.01, 100000, ErrorMessage = "Ilość składnika musi być większa od zera.")]
        public decimal Quantity { get; set; } = 1;

        [Required(ErrorMessage = "Jednostka składnika jest wymagana."), MaxLength(20)]
        public string Unit { get; set; } = "szt.";

        public bool IsOptional { get; set; }
    }
}
