using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Recipes
{
    public class CreateRecipeIngredientDto
    {
        [Required(ErrorMessage = "Nazwa składnika jest wymagana.")]
        public string Name { get; set; } = string.Empty;
        public string Quantity { get; set; } = "1 szt.";

        public Guid? ProductId { get; set; }
    }
}
