using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Jobs;

public class ExpiryNotificationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpiryNotificationWorker> _logger;

    public ExpiryNotificationWorker(IServiceProvider serviceProvider, ILogger<ExpiryNotificationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRunTime = DateTime.Today.AddHours(8); 

            if (now > nextRunTime)
            {
                nextRunTime = nextRunTime.AddDays(1);
            }

            var delay = nextRunTime - now;

            await Task.Delay(delay, stoppingToken);

            try
            {
                await ProcessExpiringItemsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania ważności produktów.");
            }
        }
    }

    private async Task ProcessExpiringItemsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GranaryDbContext>();
        var webPushService = scope.ServiceProvider.GetRequiredService<IWebPushService>(); 

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thresholdDate = today.AddDays(3);

        var expiringItems = await context.PantryItems
            .Include(p => p.Product)
            .Include(p => p.User)
            .Where(p => p.ExpirationDate <= thresholdDate)
            .ToListAsync(ct);

        foreach (var item in expiringItems)
        {
            var exists = await context.Notifications
                .AnyAsync(n => n.PantryItemId == item.Id, ct);

            if (!exists)
            {
                var daysRemaining = item.ExpirationDate.DayNumber - today.DayNumber;

                context.Notifications.Add(new Notification
                {
                    UserId = item.UserId,
                    PantryItemId = item.Id,
                    ProductName = item.Product?.Name ?? "Produkt",
                    DaysRemaining = daysRemaining,
                    IsRead = false,
                    CreatedAtUtc = DateTime.UtcNow
                });

                if (item.User.ExpiryNotifications)
                {
                    string timeText = daysRemaining <= 0 ? "dzisiaj!" : $"za {daysRemaining} dni";
                    await webPushService.SendAsync(
                        item.UserId,
                        "Granary: Ważność produktu!",
                        $"{item.Product?.Name ?? "Produkt"} traci ważność {timeText}"
                    );
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }
}