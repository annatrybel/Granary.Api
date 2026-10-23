using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Shopping
{
    public class AddShoppingItemDto
    {
        public Guid? ProductId { get; set; }

        [StringLength(200)]
        public string? CustomName { get; set; }

        [Range(0.01, 1000, ErrorMessage = "Ilość musi być większa od 0.")]
        public decimal Quantity { get; set; } = 1;

        [StringLength(20)]
        public string? Unit { get; set; } = "szt.";
    }
}
