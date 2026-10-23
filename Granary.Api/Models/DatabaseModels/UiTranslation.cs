using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels
{
    public class UiTranslation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(150)]
        public string Key { get; set; } = string.Empty; 

        [Required]
        public string Value { get; set; } = string.Empty; 
        public Guid LanguageId { get; set; }
        public Language Language { get; set; } = null!;
    }
}