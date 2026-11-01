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

    /// <summary>Dane przepisu w formie nadającej się do edycji w panelu administratora (bez tłumaczeń).</summary>
    public class AdminRecipeEditDto
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Nazwa przepisu jest wymagana."), MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Adres obrazka, data URL (Base64) z wgranego pliku lub adres wygenerowanego obrazu.</summary>
        public string? ImageUrl { get; set; }

        [Range(1, 100, ErrorMessage = "Liczba porcji musi wynosić od 1 do 100.")]
        public int Servings { get; set; } = 1;

        [Range(1, 1440, ErrorMessage = "Czas przygotowania musi wynosić od 1 do 1440 minut.")]
        public int PrepTimeMinutes { get; set; } = 15;

        [Range(0, 10000, ErrorMessage = "Kaloryczność musi wynosić od 0 do 10000 kcal.")]
        public int? Calories { get; set; }

        public List<string> Steps { get; set; } = new();

        public List<AdminRecipeIngredientDto> Ingredients { get; set; } = new();
    }
}
