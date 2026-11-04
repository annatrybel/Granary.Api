using Granary.Api.Models.Dto.Contact;
using Granary.Api.Models.Dto.Profile;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Granary.Api.Controllers.Mobile
{
    /// <summary>
    /// Zarządzanie profilem użytkownika, preferencjami, powiadomieniami i zaproszeniami do spiżarni.
    /// </summary>
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

        /// <summary>
        /// Pobiera pełne dane profilowe zalogowanego użytkownika.
        /// </summary>
        /// <returns>Dane profilu (nazwa, email, avatar, motyw, język, preferencje).</returns>
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyProfile()
        {
            var result = await _profileService.GetProfileAsync();
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Aktualizuje preferencję trybu ciemnego (Dark Mode).
        /// </summary>
        /// <param name="dto">Flaga określająca czy włączyć tryb ciemny.</param>
        [HttpPatch("dark-mode")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateDarkMode([FromBody] UpdateDarkModeDto dto)
        {
            var result = await _profileService.UpdateDarkModeAsync(dto.DarkMode);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Zmienia preferowany język interfejsu użytkownika.
        /// </summary>
        /// <param name="dto">Kod języka (np. "pl", "en").</param>
        [HttpPatch("language")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageDto dto)
        {
            var result = await _profileService.UpdateLanguageAsync(dto.Language);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Aktualizuje preferencję dietetyczną użytkownika.
        /// </summary>
        /// <param name="dto">Wybrana dieta (np. Standard, Wegetariańska, Wegańska itp.).</param>
        [HttpPatch("dietary-preference")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateDietaryPreference([FromBody] UpdateDietaryPreferenceDto dto)
        {
            var result = await _profileService.UpdateDietaryPreferenceAsync(dto.DietaryPreference);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Aktualizuje podstawowe dane profilu (nazwa użytkownika i/lub zdjęcie profilowe).
        /// </summary>
        /// <param name="dto">Formularz ze zaktualizowaną nazwą oraz opcjonalnym plikiem awatara.</param>
        /// <returns>Zaktualizowany URL awatara i nazwa użytkownika.</returns>
        [HttpPut("update-profile")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(UpdateProfileResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto dto)
        {
            var result = await _profileService.UpdateProfileAsync(dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Wysyła e-mail z zaproszeniem do wspólnej spiżarni.
        /// </summary>
        /// <remarks>
        /// Generuje bezpieczny link prowadzący do aplikacji frontendowej umożliwiający dołączenie do spiżarni.
        /// </remarks>
        /// <param name="dto">Adres e-mail zapraszanej osoby oraz nazwa spiżarni.</param>
        [HttpPost("invite-member")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> InviteMember([FromBody] SendInvitationRequestDto dto)
        {
            var result = await _profileService.SendInvitationAsync(dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Wysyła wiadomość z formularza kontaktowego do administratora serwisu.
        /// </summary>
        /// <param name="dto">Treść zgłoszenia, temat oraz dane kontaktowe.</param>
        [HttpPost("contact")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SendContactMessage([FromBody] ContactMessageDto dto)
        {
            var result = await _emailService.SendContactMessageToAdminAsync(dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Włącza lub wyłącza codzienne powiadomienia o kończącej się dacie ważności produktów.
        /// </summary>
        /// <param name="dto">Flaga określająca stan powiadomień.</param>
        [HttpPatch("notifications-settings")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateExpiryNotifications([FromBody] UpdateExpiryNotificationsDto dto)
        {
            var result = await _profileService.UpdateExpiryNotificationsAsync(dto.ExpiryNotifications);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera listę powiadomień systemowych użytkownika.
        /// </summary>
        /// <param name="onlyUnread">Jeśli true, zwraca wyłącznie nieodczytane powiadomienia.</param>
        [HttpGet("notifications")]
        [ProducesResponseType(typeof(List<NotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetNotifications([FromQuery] bool onlyUnread = true)
        {
            var result = await _profileService.GetNotificationsAsync(onlyUnread);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Oznacza wybrane powiadomienie jako odczytane.
        /// </summary>
        /// <param name="id">Identyfikator powiadomienia.</param>
        [HttpPatch("notifications/{id:guid}/read")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkNotificationAsRead([FromRoute] Guid id)
        {
            var result = await _profileService.MarkNotificationAsReadAsync(id);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Pobiera publiczny klucz VAPID wymagany do subskrypcji WebPush w przeglądarce.
        /// </summary>
        [HttpGet("push-public-key")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPushPublicKey()
        {
            var result = await _profileService.GetPushPublicKeyAsync();
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Zapisuje lub aktualizuje subskrypcję WebPush dla bieżącego urządzenia.
        /// </summary>
        /// <param name="dto">Dane subskrypcji WebPush (endpoint, klucze P256dh i Auth).</param>
        [HttpPost("push-subscription")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SavePushSubscription([FromBody] PushSubscriptionDto dto)
        {
            var result = await _profileService.SavePushSubscriptionAsync(dto);
            return HandleServiceResult(result);
        }
    }
}