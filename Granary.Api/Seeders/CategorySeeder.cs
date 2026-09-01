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
            if (await _context.Categories.AnyAsync())
            {
                return; 
            }

            var categories = new List<Category>
            {
                new() { Name = "Nabiał i Jaja", IconSlug = "dairy" },
                new() { Name = "Pieczywo", IconSlug = "bakery" },
                new() { Name = "Owoce i Warzywa", IconSlug = "veggies" },
                new() { Name = "Mięso i Wędliny", IconSlug = "meat" },
                new() { Name = "Mrożonki", IconSlug = "frozen" },
                new() { Name = "Spiżarnia i Suche", IconSlug = "pantry" }
            };

            await _context.Categories.AddRangeAsync(categories);
            await _context.SaveChangesAsync();
        }
    }
}