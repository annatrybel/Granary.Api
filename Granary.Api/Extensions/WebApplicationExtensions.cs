using Granary.Api.Models;
using Granary.Api.Seeders;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Extensions;

public static class WebApplicationExtensions
{
    public static void LoadDotEnv()
    {
        var currentDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        string? envFilePath = null;

        while (currentDirectory != null && !File.Exists(envFilePath = Path.Combine(currentDirectory.FullName, ".env")))
        {
            currentDirectory = currentDirectory.Parent;
        }

        if (envFilePath != null && File.Exists(envFilePath))
        {
            DotNetEnv.Env.Load(envFilePath);
        }
        else
        {
            Console.WriteLine("OSTRZEŻENIE: Nie znaleziono pliku .env.");
        }
    }

    public static WebApplication UseCustomMiddlewarePipeline(this WebApplication app)
    {
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        forwardedHeadersOptions.KnownNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();

        app.UseForwardedHeaders(forwardedHeadersOptions);

        var policyCollection = new HeaderPolicyCollection()
            .AddDefaultSecurityHeaders()
            .AddContentSecurityPolicy(csp =>
            {
                csp.AddDefaultSrc().Self();

                var connectSrc = csp.AddConnectSrc()
                    .Self()
                    .From("https://localhost:7128");

                var issuer = app.Configuration["JWT:Issuer"];
                if (!string.IsNullOrEmpty(issuer))
                {
                    connectSrc.From(issuer);
                }

                if (app.Environment.IsDevelopment())
                {
                    csp.AddStyleSrc().Self().UnsafeInline();
                    csp.AddScriptSrc().Self().UnsafeInline();
                }
                else
                {
                    csp.AddStyleSrc().Self();
                    csp.AddScriptSrc().Self();
                }
            })
            .AddCustomHeader("X-Permitted-Cross-Domain-Policies", "none")
            .AddPermissionsPolicy(p =>
            {
                p.AddCamera().None();
                p.AddMicrophone().None();
                p.AddGeolocation().None();
            })
            .RemoveServerHeader();

        app.UseSecurityHeaders(policyCollection);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseStaticFiles();

        app.MapHealthChecks("/healthz");
        app.MapControllers();

        //app.MapHub<ChatHub>("/chatHub");
        //app.MapHub<NotificationHub>("/notificationHub");

        return app;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<GranaryDbContext>();
        await ctx.Database.MigrateAsync();
        var seedManager = scope.ServiceProvider.GetRequiredService<SeedManager>();
        await seedManager.Seed();
    }
}