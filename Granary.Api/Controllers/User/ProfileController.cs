using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Contact;
using Granary.Api.Models.Dto.Profile;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Controllers.Mobile
{
    [ApiController]
    [Authorize]
    [Route("api/profile")]
    [Produces("application/json")]
    public class ProfileController : GranaryBaseController
    {
        private readonly IProfileService _profileService;
        private readonly IEmailService _emailService;

        public ProfileController(IProfileService profileService, IEmailService emailService)
        {
            _profileService = profileService;
            _emailService = emailService;
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = GetCurrentUserId();
            var result = await _profileService.GetProfileAsync(userId);
            return HandleServiceResult(result);
        }

        [HttpPatch("dark-mode")]
        public async Task<IActionResult> UpdateDarkMode([FromBody] UpdateDarkModeDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _profileService.UpdateDarkModeAsync(userId, dto.DarkMode);
            return HandleServiceResult(result);
        }

        [HttpPatch("language")]
        public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _profileService.UpdateLanguageAsync(userId, dto.Language);
            return HandleServiceResult(result);
        }

        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _profileService.UpdateProfileAsync(userId, dto);
            return HandleServiceResult(result);
        }

        [HttpPost("invite-member")]
        public async Task<IActionResult> InviteMember([FromBody] SendInvitationRequestDto dto)
        {
            var userId = GetCurrentUserId();
            var hostUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _profileService.SendInvitationAsync(userId, dto, hostUrl);
            return HandleServiceResult(result);
        }

        [HttpPost("contact")]
        public async Task<IActionResult> SendContactMessage(
        [FromBody] ContactMessageDto dto,
        [FromServices] IEmailService emailService)
        {
            var result = await emailService.SendContactMessageToAdminAsync(dto);
            return HandleServiceResult(result);
        }

        [HttpPatch("notifications-settings")]
        public async Task<IActionResult> UpdateExpiryNotifications([FromBody] UpdateExpiryNotificationsDto dto)
        {
            var userId = GetCurrentUserId();
            var result = await _profileService.UpdateExpiryNotificationsAsync(userId, dto.ExpiryNotifications);
            return HandleServiceResult(result);
        }

        [HttpGet("notifications")]
        [ProducesResponseType(typeof(List<NotificationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNotifications([FromQuery] bool onlyUnread = true)
        {
            var userId = GetCurrentUserId();
            var result = await _profileService.GetNotificationsAsync(userId, onlyUnread);
            return HandleServiceResult(result);
        }

        [HttpPatch("notifications/{id:guid}/read")]
        public async Task<IActionResult> MarkNotificationAsRead([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _profileService.MarkNotificationAsReadAsync(userId, id);
            return HandleServiceResult(result);
        }

        [HttpPost("push-subscription")]
        public async Task<IActionResult> SavePushSubscription([FromBody] PushSubscriptionDto dto, [FromServices] GranaryDbContext db)
        {
            var userId = GetCurrentUserId();

            var exists = await db.DevicePushSubscriptions
                .AnyAsync(s => s.UserId == userId && s.Endpoint == dto.Endpoint);

            if (!exists)
            {
                db.DevicePushSubscriptions.Add(new DevicePushSubscription
                {
                    UserId = userId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.P256dh,
                    Auth = dto.Auth
                });
                await db.SaveChangesAsync();
            }

            return NoContent();
        }
    }
}