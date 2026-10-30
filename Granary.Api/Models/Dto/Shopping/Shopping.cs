using System.ComponentModel.DataAnnotations;
using Granary.Api.Models.Enums;

namespace Granary.Api.Models.Dto.Shopping
{
    public class ShoppingItemDto
    {
        public Guid Id { get; set; }
        public Guid? ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Emoji { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public bool IsBought { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateShoppingItemDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [Range(0.01, 1000, ErrorMessage = "Ilość musi być większa od 0.")]
        public decimal? Quantity { get; set; }

        [StringLength(20)]
        public string? Unit { get; set; }

        public bool? IsBought { get; set; }
    }

    

    public class ShoppingListDetailDto : ShoppingListSummaryDto
    {
        public List<ShoppingItemDto> Items { get; set; } = new();
    } 

}