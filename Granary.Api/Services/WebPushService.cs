using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebPush;

namespace Granary.Api.Services;

public interface IWebPushService
{
    Task SendAsync(Guid userId, string title, string message);
}

public class WebPushService : IWebPushService
{
    private readonly GranaryDbContext _context;
    private readonly IConfiguration _configuration;

    private readonly ILogger<WebPushService> _logger;

    public WebPushService(GranaryDbContext context, IConfiguration configuration, ILogger<WebPushService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(Guid userId, string title, string message)
    {
        var subscriptions = await _context.DevicePushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        if (!subscriptions.Any())
        {
            _logger.LogInformation("Brak subskrypcji push dla użytkownika {UserId}.", userId);
            return;
        }

        var subject = _configuration["VAPID_SUBJECT"] ?? "mailto:admin@granary.pl";
        var publicKey = _configuration["VAPID_PUBLIC_KEY"];
        var privateKey = _configuration["VAPID_PRIVATE_KEY"];

        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
        {
            _logger.LogWarning("Brak konfiguracji VAPID_PUBLIC_KEY / VAPID_PRIVATE_KEY - push nie zostanie wysłany.");
            return;
        }

        var vapidDetails = new VapidDetails(subject, publicKey, privateKey);
        var client = new WebPushClient();
        // Angular Service Worker (SwPush) wymaga formatu { notification: { ... } }
        var payload = JsonSerializer.Serialize(new
        {
            notification = new
            {
                title,
                body = message,
                icon = "/icons/icon-192.png",
                data = new { url = "/" }
            }
        });

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                _context.DevicePushSubscriptions.Remove(sub);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się wysłać push do {Endpoint}.", sub.Endpoint);
            }
        }

        await _context.SaveChangesAsync();
    }
}