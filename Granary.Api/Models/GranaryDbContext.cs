using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Models
{
    public class GranaryDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public GranaryDbContext(DbContextOptions<GranaryDbContext> options) : base(options)
        {
        }

        public GranaryDbContext() { }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<PantryItem> PantryItems { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeIngredient> RecipeIngredients { get; set; }
        public DbSet<MealPlan> MealPlans { get; set; }
        public DbSet<MealPlanRecipe> MealPlanRecipes { get; set; }
        public DbSet<ShoppingList> ShoppingLists { get; set; }
        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserDto>().HasNoKey().ToView("UserWithRoles");

            
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode);
            modelBuilder.Entity<PantryItem>()
                .HasIndex(p => new { p.UserId, p.ExpirationDate });
            modelBuilder.Entity<PantryItem>()
                .Property(p => p.StorageLocation)
                .HasConversion<string>();

            modelBuilder.Entity<PantryItem>()
                .Property(p => p.Status)
                .HasConversion<string>();

            modelBuilder.Entity<MealPlan>()
                .Property(m => m.Status)
                .HasConversion<string>();

            modelBuilder.Entity<MealPlanRecipe>()
                .Property(m => m.MealType)
                .HasConversion<string>();
        }
    }
}