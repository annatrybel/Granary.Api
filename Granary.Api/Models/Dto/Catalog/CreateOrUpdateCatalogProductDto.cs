using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto
{
    public class CreateOrUpdateCatalogProductDto
    {
        [Required(ErrorMessage = "Nazwa produktu jest wymagana.")]
        [StringLength(100, ErrorMessage = "Nazwa produktu nie może przekraczać 100 znaków.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Kod kreskowy nie może przekraczać 50 znaków.")]
        public string? Barcode { get; set; }

        public string? ImageUrl { get; set; }

        [StringLength(20, ErrorMessage = "Jednostka miary nie może przekraczać 20 znaków.")]
        public string? DefaultUnit { get; set; } = "szt.";

        [Range(1, 3650, ErrorMessage = "Domyślny okres ważności musi wynosić od 1 do 3650 dni.")]
        public int DefaultShelfLifeDays { get; set; } = 7;

        [Required(ErrorMessage = "Kategoria produktu jest wymagana.")]
        public Guid CategoryId { get; set; }
    }
}