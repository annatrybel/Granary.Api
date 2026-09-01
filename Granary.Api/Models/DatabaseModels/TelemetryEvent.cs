using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels
{
    public class TelemetryEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(64)]
        public string SessionHash { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string EventType { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? RoutePath { get; set; }

        // Core Web Vitals Metrics
        public decimal? CwvLcp { get; set; }
        public decimal? CwvFid { get; set; }
        public decimal? CwvCls { get; set; }

        public int? ExecutionTimeMs { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
