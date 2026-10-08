// Controllers/ProfileController.cs
using System.Security.Claims;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Profile;
using Granary.Api.Models.Dto.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers
{
    [ApiController]
    [Authorize] 
    [Route("api/profile")]
    [Produces("application/json")]
    public class ProfileController : GranaryBaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Pobiera dane profilowe aktualnie zalogowanego użytkownika.
        /// </summary>
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyProfile()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var dto = new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                DarkMode = user.DarkMode
            };

            return Ok(dto);
        }

        /// <summary>
        /// Szybka zmiana preferencji motywu (Jasny / Ciemny).
        /// </summary>
        [HttpPatch("dark-mode")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateDarkMode([FromBody] UpdateDarkModeDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            user.DarkMode = dto.DarkMode;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest("Nie udało się zaktualizować preferencji motywu.");
            }

            return NoContent();
        }

        /// <summary>
        /// Aktualizacja danych profilu (avatar, username).
        /// </summary>
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                user.UserName = dto.Username;
            }


            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest("Nie udało się zaktualizować profilu.");
            }

            return Ok(new { message = "Profil zaktualizowany pomyślnie" });
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return null;

            return await _userManager.FindByIdAsync(userIdString);
        }
    }
}