using Granary.Api.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.Dto.Shopping
{
    public class BulkTransferToPantryDto
    {
        [Required(ErrorMessage = "Musisz zaznaczyć przynajmniej jeden produkt.")]
        public List<Guid> ItemIds { get; set; } = new();

        [Required(ErrorMessage = "Wybierz miejsce docelowe (Lodówka, Zamrażarka, Spiżarnia).")]
        public StorageLocation DestinationLocation { get; set; } = StorageLocation.Fridge;

        public DateOnly? DefaultExpirationDate { get; set; }
    }
}
