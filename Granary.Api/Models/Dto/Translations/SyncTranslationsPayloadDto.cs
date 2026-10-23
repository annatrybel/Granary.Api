namespace Granary.Api.Models.Dto.Translations
{
    public class SyncTranslationsPayloadDto
    {
        public List<TranslationKeyItemDto> Keys { get; set; } = new();

        public bool PruneStale { get; set; } = true;
    }

    public class TranslationKeyItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
    }

    public class SyncTranslationsResultDto
    {
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Pruned { get; set; }
    }
}