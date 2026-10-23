using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels;

public class DevicePushSubscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    [Required] public string Endpoint { get; set; } = string.Empty;
    [Required] public string P256dh { get; set; } = string.Empty;
    [Required] public string Auth { get; set; } = string.Empty;
}