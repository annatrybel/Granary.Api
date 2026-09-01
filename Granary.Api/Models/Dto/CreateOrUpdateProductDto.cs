using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto
{
    public class CreateOrUpdateProductDto
    {
        [Required(ErrorMessage = "Identyfikator kategorii jest wymagany.")]
        public Guid CategoryId { get; set; }

        [Required(ErrorMessage = "Nazwa produktu jest wymagana.")]
        [StringLength(200, ErrorMessage = "Nazwa produktu nie może przekraczać 200 znaków.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Kod kreskowy nie może przekraczać 50 znaków.")]
        public string? Barcode { get; set; }

        [Required(ErrorMessage = "Domyślna jednostka jest wymagana.")]
        [StringLength(20, ErrorMessage = "Nazwa jednostki nie może przekraczać 20 znaków.")]
        public string DefaultUnit { get; set; } = "pcs";

        [Range(1, 3650, ErrorMessage = "Domyślny okres przydatności musi mieścić się w przedziale od 1 do 3650 dni.")]
        public int? DefaultShelfLifeDays { get; set; }

        [MaxLength(10)]
        public string? Emoji { get; set; }
    }
}
