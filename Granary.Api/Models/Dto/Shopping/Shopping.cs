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

    public class ShoppingListSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public DateOnly? TargetDate { get; set; }
        public int TotalItemsCount { get; set; }
        public int CompletedItemsCount { get; set; }
    }

    public class ShoppingListDetailDto : ShoppingListSummaryDto
    {
        public List<ShoppingItemDto> Items { get; set; } = new();
    }

    public class CreateShoppingListDto
    {
        [Required(ErrorMessage = "Nazwa listy jest wymagana.")]
        [StringLength(100, ErrorMessage = "Nazwa nie może przekraczać 100 znaków.")]
        public string Name { get; set; } = string.Empty;

        public DateOnly? TargetDate { get; set; }
    }

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

    public class BulkTransferToPantryDto
    {
        [Required(ErrorMessage = "Musisz zaznaczyć przynajmniej jeden produkt.")]
        public List<Guid> ItemIds { get; set; } = new();

        [Required(ErrorMessage = "Wybierz miejsce docelowe (Lodówka, Zamrażarka, Spiżarnia).")]
        public StorageLocation DestinationLocation { get; set; } = StorageLocation.Fridge;

        public DateOnly? DefaultExpirationDate { get; set; }
    }
}