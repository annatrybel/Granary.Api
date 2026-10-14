using Granary.Api.Models.Dto.Contact;
using Granary.Api.Models.Dto.Profile;
using Granary.Api.Services;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        /// <summary>
        /// Odbiera zgłoszenie użytkownika z Centrum Pomocy i wysyła je na skrzynkę e-mail administratora.
        /// </summary>
        [HttpPost]
        [AllowAnonymous] 
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendMessage([FromBody] ContactMessageDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _emailService.SendContactMessageToAdminAsync(dto);
            return HandleServiceResult(result);
        }
    }
}