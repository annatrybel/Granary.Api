namespace Granary.Api.Models.Dto.Shopping
{
    public class ShoppingListSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public DateOnly? TargetDate { get; set; }
        public int TotalItemsCount { get; set; }
        public int CompletedItemsCount { get; set; }
    }
}
