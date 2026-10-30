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
        public DbSet<MealPlan> MealPlans { get; set; }     //przemyslec i jak cos to do usuniecia
        public DbSet<MealPlanRecipe> MealPlanRecipes { get; set; }  //przemyslec i jak cos to do usuniecia
        public DbSet<ShoppingList> ShoppingLists { get; set; }
        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }
        public DbSet<UserRecipeInteraction> UserRecipeInteractions { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<UiTranslation> UiTranslations { get; set; }
        public DbSet<ProductTranslation> ProductTranslations { get; set; }
        public DbSet<RecipeTranslation> RecipeTranslations { get; set; }
        public DbSet<CategoryTranslation> CategoryTranslations { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<DevicePushSubscription> DevicePushSubscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("pg_trgm");

            modelBuilder.Entity<UserDto>().HasNoKey().ToView("UserWithRoles");

            // Indeksy trigramowe przyspieszają wyszukiwanie "zawiera" (ILIKE '%tekst%'),
            // którego zwykły indeks B-tree nie obsługuje.
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Name)
                .HasDatabaseName("IX_Products_Name_trgm")
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            modelBuilder.Entity<ProductTranslation>()
                .HasIndex(pt => pt.Name)
                .HasDatabaseName("IX_ProductTranslations_Name_trgm")
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            modelBuilder.Entity<Recipe>()
                .HasIndex(r => r.Title)
                .HasDatabaseName("IX_Recipes_Title_trgm")
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            modelBuilder.Entity<RecipeTranslation>()
                .HasIndex(rt => rt.Title)
                .HasDatabaseName("IX_RecipeTranslations_Title_trgm")
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            
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

            modelBuilder.Entity<UserRecipeInteraction>()
                .HasKey(x => new { x.UserId, x.RecipeId });

            modelBuilder.Entity<UserRecipeInteraction>()
                .HasOne(x => x.Recipe)
                .WithMany()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unikalność kodu języka (np. tylko jedno "pl")
            modelBuilder.Entity<Language>()
                .HasIndex(l => l.Code)
                .IsUnique();

            // Klucz tłumaczenia + Język muszą być unikalne
            modelBuilder.Entity<UiTranslation>()
                .HasIndex(t => new { t.Key, t.LanguageId })
                .IsUnique();

            // Unikalność tłumaczenia produktu dla konkretnego języka
            modelBuilder.Entity<ProductTranslation>()
                .HasIndex(pt => new { pt.ProductId, pt.LanguageId })
                .IsUnique();

            // Unikalność tłumaczenia przepisu dla konkretnego języka
            modelBuilder.Entity<RecipeTranslation>()
                .HasIndex(rt => new { rt.RecipeId, rt.LanguageId })
                .IsUnique();

            modelBuilder.Entity<CategoryTranslation>()
                .HasIndex(ct => new { ct.CategoryId, ct.LanguageId })
                .IsUnique();
        }
    }
}