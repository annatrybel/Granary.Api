using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Recipes
{
    public class AdminRecipeEditDto
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Nazwa przepisu jest wymagana."), MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        [Range(1, 100, ErrorMessage = "Liczba porcji musi wynosić od 1 do 100.")]
        public int Servings { get; set; } = 1;

        [Range(1, 1440, ErrorMessage = "Czas przygotowania musi wynosić od 1 do 1440 minut.")]
        public int PrepTimeMinutes { get; set; } = 15;

        [Range(0, 10000, ErrorMessage = "Kaloryczność musi wynosić od 0 do 10000 kcal.")]
        public int? Calories { get; set; }

        /// <summary>Identyfikatory GUID wybranych diet.</summary>
        public List<Guid> DietIds { get; set; } = new();

        public List<string> Steps { get; set; } = new();
        public List<AdminRecipeIngredientDto> Ingredients { get; set; } = new();
    }
}