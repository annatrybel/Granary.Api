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
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    

    public class ShoppingListDetailDto : ShoppingListSummaryDto
    {
        public List<ShoppingItemDto> Items { get; set; } = new();
    } 

}