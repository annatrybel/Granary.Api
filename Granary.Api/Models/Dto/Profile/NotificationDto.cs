namespace Granary.Api.Models.Dto.Profile;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid? PantryItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int DaysRemaining { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}