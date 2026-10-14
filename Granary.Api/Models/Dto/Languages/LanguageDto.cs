namespace Granary.Api.Models.Dto.Languages
{
    public record LanguageDto(Guid Id, string Code, string Name, string FlagEmoji, bool IsDefault);
}
