using Granary.Api.Extensions;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// ==========================================
// ŁADOWANIE KONFIGURACJI (.env)
// ==========================================
WebApplicationExtensions.LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// BAZA DANYCH I IDENTITY
// ==========================================
string? rawConnectionString = builder.Configuration.GetValue<bool>("IS_IN_CONTAINER")
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : builder.Configuration.GetConnectionString("DefaultConnection_LOCAL");

var connectionString = ConnectionStringConverter.Convert(rawConnectionString);

builder.Services.AddDbContext<GranaryDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<GranaryDbContext>()
    .AddDefaultTokenProviders();

// ==========================================
// REJESTRACJA METOD ROZSZERZAJĄCYCH (DI)
// ==========================================
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddCustomCors(builder.Configuration);
builder.Services.AddCustomRateLimiter();
builder.Services.AddCustomSwagger();
builder.Services.AddBusinessServices();

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"] ?? Environment.GetEnvironmentVariable("Sentry__Dsn");
    options.Debug = true;
    options.TracesSampleRate = 1.0;
});

// ==========================================
// PIPELINE APLIKACJI & INICJALIZACJA
// ==========================================
var app = builder.Build();

app.UseCustomMiddlewarePipeline();

await app.InitializeDatabaseAsync();

app.Run();