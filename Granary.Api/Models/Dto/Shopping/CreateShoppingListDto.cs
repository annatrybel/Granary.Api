using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Shopping
{
    public class CreateShoppingListDto
    {
        [Required(ErrorMessage = "Nazwa listy jest wymagana.")]
        [StringLength(100, ErrorMessage = "Nazwa nie może przekraczać 100 znaków.")]
        public string Name { get; set; } = string.Empty;

        public DateOnly? TargetDate { get; set; }
    }
}
