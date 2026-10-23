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

    public WebPushService(GranaryDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task SendAsync(Guid userId, string title, string message)
    {
        var subscriptions = await _context.DevicePushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        if (!subscriptions.Any()) return;

        var subject = _configuration["VAPID_SUBJECT"] ?? "mailto:admin@granary.pl";
        var publicKey = _configuration["VAPID_PUBLIC_KEY"];
        var privateKey = _configuration["VAPID_PRIVATE_KEY"];

        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey)) return;

        var vapidDetails = new VapidDetails(subject, publicKey, privateKey);
        var client = new WebPushClient();
        var payload = JsonSerializer.Serialize(new { title, body = message });

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                _context.DevicePushSubscriptions.Remove(sub);
            }
            catch {  }
        }

        await _context.SaveChangesAsync();
    }
}