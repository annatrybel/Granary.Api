using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Seeders
{
    public class ProductSeeder
    {
        private readonly GranaryDbContext _context;

        public ProductSeeder(GranaryDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            if (await _context.Products.AnyAsync())
            {
                return;
            }

            var categories = await _context.Categories
                .Where(c => c.IconSlug != null)
                .ToDictionaryAsync(c => c.IconSlug!, c => c.Id);

            if (!categories.Any())
            {
                return; 
            }

            var products = new List<Product>
            {
                new() { CategoryId = categories["dairy"], Name = "Mleko 3.2%", Emoji = "🥛", DefaultUnit = "L", DefaultShelfLifeDays = 7, Barcode = "5900820000018" },
                new() { CategoryId = categories["dairy"], Name = "Jajka (M/L)", Emoji = "🥚", DefaultUnit = "pcs", DefaultShelfLifeDays = 21 },
                new() { CategoryId = categories["dairy"], Name = "Ser Żółty", Emoji = "🧀", DefaultUnit = "g", DefaultShelfLifeDays = 14 },
                new() { CategoryId = categories["dairy"], Name = "Masło Extra", Emoji = "🧈", DefaultUnit = "pcs", DefaultShelfLifeDays = 30 },

                new() { CategoryId = categories["bakery"], Name = "Chleb Pszenno-Żytni", Emoji = "🍞", DefaultUnit = "pcs", DefaultShelfLifeDays = 3 },
                new() { CategoryId = categories["bakery"], Name = "Bułka Kajzerka", Emoji = "🥖", DefaultUnit = "pcs", DefaultShelfLifeDays = 1 },

                new() { CategoryId = categories["veggies"], Name = "Jabłka", Emoji = "🍎", DefaultUnit = "kg", DefaultShelfLifeDays = 14 },
                new() { CategoryId = categories["veggies"], Name = "Pomidory", Emoji = "🍅", DefaultUnit = "kg", DefaultShelfLifeDays = 5 },

                new() { CategoryId = categories["frozen"], Name = "Pizza Mrożona", Emoji = "🍕", DefaultUnit = "pcs", DefaultShelfLifeDays = 180 },

                new() { CategoryId = categories["pantry"], Name = "Mąka Pszenna", Emoji = "🌾", DefaultUnit = "kg", DefaultShelfLifeDays = 365 },
                new() { CategoryId = categories["pantry"], Name = "Olej Rzepakowy", Emoji = "🍾", DefaultUnit = "L", DefaultShelfLifeDays = 180 }
            };

            await _context.Products.AddRangeAsync(products);
            await _context.SaveChangesAsync();
        }
    }
}