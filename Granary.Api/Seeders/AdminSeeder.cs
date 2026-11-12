using Granary.Api.Models.DatabaseModels;
using Microsoft.AspNetCore.Identity;

namespace Granary.Api.Seeders
{
    public class AdminSeeder
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminSeeder> _logger;

        public AdminSeeder(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IConfiguration configuration,
            ILogger<AdminSeeder> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SeedAdminAsync()
        {
            const string adminRole = "Administrator";

            if (!await _roleManager.RoleExistsAsync(adminRole))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));
            }

            var existingAdmins = await _userManager.GetUsersInRoleAsync(adminRole);
            if (existingAdmins.Count > 0)
            {
                return;
            }

            var adminEmail = _configuration["ADMIN_EMAIL"]
                          ?? _configuration["AdminCredentials:Email"]
                          ?? "admin@granary.local";

            var adminUsername = _configuration["ADMIN_USERNAME"]
                             ?? _configuration["AdminCredentials:Username"]
                             ?? "admin";

            var adminPassword = _configuration["ADMIN_PASSWORD"]
                             ?? _configuration["AdminCredentials:Password"]
                             ?? "Admin123!";

            var existingUser = await _userManager.FindByEmailAsync(adminEmail);
            if (existingUser == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminUsername,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                };

                var createResult = await _userManager.CreateAsync(adminUser, adminPassword);
                if (createResult.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, adminRole);
                    _logger.LogInformation("Administrator startowy ({Email}) został pomyślnie utworzony.", adminEmail);
                }
                else
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    _logger.LogError("Błąd podczas tworzenia administratora startowego: {Errors}", errors);
                }
            }
            else
            {
                if (!await _userManager.IsInRoleAsync(existingUser, adminRole))
                {
                    await _userManager.AddToRoleAsync(existingUser, adminRole);
                    existingUser.EmailConfirmed = true;
                    await _userManager.UpdateAsync(existingUser);
                    _logger.LogInformation("Istniejący użytkownik ({Email}) otrzymał uprawnienia Administratora.", adminEmail);
                }
            }
        }
    }
}