using System.Text.Json;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Seeders
{
    public class DietSeeder
    {
        private readonly GranaryDbContext _context;
        private readonly ILogger<DietSeeder> _logger;

        public DietSeeder(GranaryDbContext context, ILogger<DietSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            if (await _context.Diets.AnyAsync())
            {
                return; 
            }

            var filePath = Path.Combine(AppContext.BaseDirectory, "Seeders", "Data", "diets.json");

            if (!File.Exists(filePath))
            {
                filePath = Path.Combine(Directory.GetCurrentDirectory(), "Seeders", "Data", "diets.json");
            }

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Plik diets.json nie został znaleziony pod ścieżką: {Path}", filePath);
                return;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var diets = JsonSerializer.Deserialize<List<Diet>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (diets != null && diets.Any())
                {
                    await _context.Diets.AddRangeAsync(diets);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Pomyślnie zasiano {Count} diet do bazy.", diets.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wystąpił błąd podczas seedowania diet.");
            }
        }
    }
}