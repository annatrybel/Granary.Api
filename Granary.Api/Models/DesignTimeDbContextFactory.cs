using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Granary.Api.Models
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GranaryDbContext>
    {
        public GranaryDbContext CreateDbContext(string[] args)
        {
            var envPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, ".env");
            if (File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
            }

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables() 
                .Build();

            var builder = new DbContextOptionsBuilder<GranaryDbContext>();

            var connectionString = configuration.GetConnectionString("DefaultConnection_LOCAL");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Nie można odnaleźć connection stringa 'DefaultConnection_LOCAL' dla narzędzi Design-Time. " +
                    "Upewnij się, że jest on zdefiniowany w pliku .env."
                );
            }

            builder.UseNpgsql(connectionString);

            return new GranaryDbContext(builder.Options);
        }
    }
}
