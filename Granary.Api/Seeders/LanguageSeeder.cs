using System.Text.Json;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Seeders
{
    public class LanguageSeeder
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<LanguageSeeder> _logger;

        public LanguageSeeder(GranaryDbContext context, ILogger<LanguageSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                if (!await _context.Languages.AnyAsync())
                {
                    var languagesPath = ResolveFilePath("Languages.json");
                    _logger.LogInformation("Wczytywanie języków ze ścieżki: {Path}", languagesPath);

                    var json = await File.ReadAllTextAsync(languagesPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var languagesData = JsonSerializer.Deserialize<List<LanguageSeedModel>>(json, options);

                    if (languagesData != null && languagesData.Any())
                    {
                        var languages = languagesData.Select(l => new Language
                        {
                            Id = Guid.NewGuid(),
                            Code = l.Code,
                            Name = l.Name,
                            FlagEmoji = l.FlagEmoji,
                            IsActive = l.IsActive,
                            IsDefault = l.IsDefault,
                            DisplayOrder = l.DisplayOrder
                        }).ToList();

                        await _context.Languages.AddRangeAsync(languages);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Pomyślnie zaseedowano {Count} języków.", languages.Count);
                    }
                }

                var allLanguages = await _context.Languages.ToListAsync();

                foreach (var lang in allLanguages)
                {
                    var fileName = lang.Code.ToLower() == "pl"
                        ? "UiTranslations.json"
                        : $"UiTranslations_{lang.Code.ToLower()}.json";

                    string translationsPath;
                    try
                    {
                        translationsPath = ResolveFilePath(fileName);
                    }
                    catch (FileNotFoundException)
                    {
                        _logger.LogWarning("Brak dedykowanego pliku tłumaczeń {File} dla języka {Lang} - tłumaczenia zostaną uzupełnione przez AI.", fileName, lang.Code);
                        continue;
                    }

                    var json = await File.ReadAllTextAsync(translationsPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var translationsData = JsonSerializer.Deserialize<List<TranslationSeedModel>>(json, options);

                    if (translationsData != null && translationsData.Any())
                    {
                        var existingKeys = await _context.UiTranslations
                            .Where(t => t.LanguageId == lang.Id)
                            .Select(t => t.Key)
                            .ToHashSetAsync();

                        var newTranslations = translationsData
                            .Where(t => !existingKeys.Contains(t.Key))
                            .Select(t => new UiTranslation
                            {
                                Id = Guid.NewGuid(),
                                Key = t.Key,
                                Value = t.Value,
                                LanguageId = lang.Id
                            }).ToList();

                        if (newTranslations.Any())
                        {
                            await _context.UiTranslations.AddRangeAsync(newTranslations);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Dopisano {Count} nowych tłumaczeń dla języka {Lang} ({Name}).", newTranslations.Count, lang.Code, lang.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wystąpił błąd podczas seedowania języków i tłumaczeń.");
            }
        }

        private static string ResolveFilePath(string fileName)
        {
            var basePath = Path.Combine(AppContext.BaseDirectory, "Seeders", "Data", fileName);
            if (File.Exists(basePath)) return basePath;

            var currentPath = Path.Combine(Directory.GetCurrentDirectory(), "Seeders", "Data", fileName);
            if (File.Exists(currentPath)) return currentPath;

            throw new FileNotFoundException($"Nie odnaleziono pliku: {fileName}. Szukano w: {basePath} oraz {currentPath}");
        }

        private record LanguageSeedModel(string Code, string Name, string FlagEmoji, bool IsActive, bool IsDefault, int DisplayOrder);
        private record TranslationSeedModel(string Key, string Value);
    }
}