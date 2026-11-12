namespace Granary.Api.Models.DatabaseModels
{
    public class DietTranslation
    {
        public Guid DietId { get; set; }
        public Diet Diet { get; set; } = null!;

        public Guid LanguageId { get; set; }
        public Language Language { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
    }
}