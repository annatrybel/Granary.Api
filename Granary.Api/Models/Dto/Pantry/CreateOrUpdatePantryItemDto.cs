using Granary.Api.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto
{
    public class CreateOrUpdatePantryItemDto
    {
        public Guid? ProductId { get; set; }

        [StringLength(200)]
        public string? CustomProductName { get; set; }
        public Guid? CustomCategoryId { get; set; }
        public string? CustomImageUrl { get; set; }

        [Required(ErrorMessage = "Lokalizacja jest wymagana.")]
        public StorageLocation Location { get; set; } = StorageLocation.Fridge;

        [Range(0.01, 1000, ErrorMessage = "Ilość musi być większa od 0.")]
        public decimal Quantity { get; set; } = 1;

        public string? Unit { get; set; }
        public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Required(ErrorMessage = "Data ważności jest wymagana.")]
        public DateOnly ExpirationDate { get; set; }
    }
}