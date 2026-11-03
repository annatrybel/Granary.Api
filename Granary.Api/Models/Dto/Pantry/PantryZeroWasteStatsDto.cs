namespace Granary.Api.Models.Dto.Pantry
{
    public class PantryZeroWasteStatsDto
    {
        public int ConsumedCount { get; set; }
        public int WastedCount { get; set; }
        public decimal Percentage { get; set; }
    }
}
