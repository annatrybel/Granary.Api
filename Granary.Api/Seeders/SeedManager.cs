namespace Granary.Api.Seeders
{
    public class SeedManager
    {
        private readonly RoleSeeder _roleSeeder;
        private readonly CategorySeeder _categorySeeder;
        private readonly ProductSeeder _productSeeder;

        public SeedManager(
            RoleSeeder roleSeeder,
            CategorySeeder categorySeeder,
            ProductSeeder productSeeder)
        {
            _roleSeeder = roleSeeder;
            _categorySeeder = categorySeeder;
            _productSeeder = productSeeder;
        }

        public async Task Seed()
        {
            await _roleSeeder.SeedRolesAsync();
            await _categorySeeder.SeedAsync(); 
            await _productSeeder.SeedAsync();  
        }
    }
}