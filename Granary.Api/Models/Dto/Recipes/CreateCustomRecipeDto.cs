using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Recipes
{
    public class CreateCustomRecipeDto
    {
        [Required(ErrorMessage = "Nazwa przepisu jest wymagana."), MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? Category { get; set; }

        public string? ImageUrl { get; set; }

        [Range(1, 100, ErrorMessage = "Liczba porcji musi wynosić co najmniej 1.")]
        public int Servings { get; set; } = 1;

        [Range(1, 1440, ErrorMessage = "Czas przygotowania musi wynosić co najmniej 1 minutę.")]
        public int PrepTimeMinutes { get; set; } = 15;

        public List<CreateRecipeIngredientDto> Ingredients { get; set; } = new();

        public List<string> Steps { get; set; } = new();
    }

    public class CreateRecipeIngredientDto
    {
        [Required(ErrorMessage = "Nazwa składnika jest wymagana.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Tekst wpisany przez użytkownika np. "1 kg", "500 g", "2 szt."
        /// </summary>
        public string Quantity { get; set; } = "1 szt.";

        public Guid? ProductId { get; set; }
    }
}