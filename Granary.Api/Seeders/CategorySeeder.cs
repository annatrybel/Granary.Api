using System.Text.Json;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Seeders
{
    public class CategorySeeder
    {
        private readonly GranaryDbContext _context;

        public CategorySeeder(GranaryDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            if (await _context.Categories.AnyAsync()) return;

            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Seeders", "Data", "categories.json");
            if (!File.Exists(jsonPath))
            {
                jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Seeders", "Data", "categories.json");
            }
            if (!File.Exists(jsonPath)) return;

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var categories = JsonSerializer.Deserialize<List<Category>>(jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (categories != null && categories.Any())
            {
                await _context.Categories.AddRangeAsync(categories);
                await _context.SaveChangesAsync();
            }
        }
    }
}