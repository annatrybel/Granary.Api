using Granary.Api.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels
{
    public class SystemAnomaly
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string AnomalyType { get; set; } = string.Empty;

        public AnomalySeverity Severity { get; set; } = AnomalySeverity.Medium;

        [Required]
        public string Description { get; set; } = string.Empty;

        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; } = false;
    }
}
