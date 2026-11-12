namespace Granary.Api.Seeders
{
    public class SeedManager
    {
        private readonly RoleSeeder _roleSeeder;
        private readonly AdminSeeder _adminSeeder;
        private readonly LanguageSeeder _languageSeeder;
        private readonly CategorySeeder _categorySeeder;
        private readonly DietSeeder _dietSeeder;
        private readonly ProductSeeder _productSeeder;
        private readonly RecipeSeeder _recipeSeeder;

        public SeedManager(
            RoleSeeder roleSeeder,
            AdminSeeder adminSeeder,
            LanguageSeeder languageSeeder,
            CategorySeeder categorySeeder,
            DietSeeder dietSeeder,
            ProductSeeder productSeeder,
            RecipeSeeder recipeSeeder)
        {
            _roleSeeder = roleSeeder;
            _adminSeeder = adminSeeder;
            _languageSeeder = languageSeeder;
            _categorySeeder = categorySeeder;
            _dietSeeder = dietSeeder;
            _productSeeder = productSeeder;
            _recipeSeeder = recipeSeeder;
        }

        public async Task Seed()
        {
            await _roleSeeder.SeedRolesAsync();
            await _adminSeeder.SeedAdminAsync();
            await _languageSeeder.SeedAsync();
            await _categorySeeder.SeedAsync();
            await _dietSeeder.SeedAsync();
            await _productSeeder.SeedAsync();
            await _recipeSeeder.SeedAsync();
        }
    }
}