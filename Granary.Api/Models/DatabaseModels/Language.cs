using System.ComponentModel.DataAnnotations;

namespace Granary.Api.Models.DatabaseModels
{
    public class Language
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty; 

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty; 

        [MaxLength(10)]
        public string FlagEmoji { get; set; } = string.Empty; 

        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false; 
        public int DisplayOrder { get; set; } = 0;

        public ICollection<UiTranslation> Translations { get; set; } = new List<UiTranslation>();
    }
}
