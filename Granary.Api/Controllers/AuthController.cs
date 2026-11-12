using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Auth;
using Granary.Api.Services.Auth;
using Granary.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;

namespace Granary.Api.Controllers
{
    /// <summary>
    /// Kontroler odpowiedzialny za uwierzytelnianie, rejestrację, cykl życia tokenów JWT oraz integracje OAuth.
    /// </summary>
    /// <remarks>
    /// Działa w modelu Thin Controller – deleguje całą logikę biznesową do IAuthService.
    /// Zwraca zunifikowane odpowiedzi zgodne ze standardem RFC 7807 ProblemDetails w przypadku błędów.
    /// </remarks>
    [ApiController]
    [Route("api/authentication")]
    [Produces("application/json")]
    public class AuthController : GranaryBaseController
    {
        private readonly IAuthService _authService;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;

        public AuthController(
            IAuthService authService,
            IConfiguration configuration,
            SignInManager<ApplicationUser> signInManager)
        {
            _authService = authService;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        /// <summary>
        /// Inicjuje rejestrację nowego użytkownika (Wariant bez hasła - Email First).
        /// </summary>
        /// <remarks>
        /// Endpoint zabezpieczony przed enumeracją (Silent Flow) – zawsze zwraca status 200 OK.
        /// Jeśli adres nie istnieje w bazie, tworzy konto i wysyła e-mail z linkiem do utworzenia hasła.
        /// Jeśli konto już istnieje, nie ujawnia tej informacji na zewnątrz.
        /// </remarks>
        /// <param name="registerDto">Model zawierający adres e-mail kandydata.</param>
        /// <response code="200">Wiadomość z linkiem została wysłana (lub konto już istnieje - silent flow).</response>
        /// <response code="400">Błędny format adresu e-mail.</response>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var result = await _authService.RegisterAsync(registerDto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Potwierdza adres e-mail użytkownika na podstawie tokena aktywacyjnego.
        /// </summary>
        /// <param name="confirmEmailDto">Adres e-mail oraz unikalny token przesłany w wiadomości.</param>
        /// <response code="200">Konto zostało pomyślnie aktywowane.</response>
        /// <response code="400">Token aktywacyjny jest nieprawidłowy lub wygasł.</response>
        [HttpPost("confirm-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto confirmEmailDto)
        {
            var result = await _authService.ConfirmEmailAsync(confirmEmailDto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Logowanie do aplikacji użytkownika za pomocą E-maila LUB Nazwy użytkownika.
        /// </summary>
        /// <remarks>
        /// W przypadku sukcesu wystawia parę tokenów:
        /// - Access Token (JWT): krótki czas życia (15 minut).
        /// - Refresh Token: długi czas życia (14 dni), zapisany w bazie danych.
        /// Endpoint objęty polityką ograniczenia zapytań (maks. 5 prób/min).
        /// </remarks>
        /// <param name="loginDto">Identyfikator (login/e-mail) oraz hasło.</param>
        /// <response code="200">Pomyślne logowanie. Zwraca token dostępowy oraz token odświeżający.</response>
        /// <response code="400">Nieprawidłowe dane logowania lub konto nie zostało jeszcze aktywowane.</response>
        /// <response code="429">Zbyt wiele nieudanych prób logowania (Rate Limit).</response>
        [EnableRateLimiting("LoginPolicy")]
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var result = await _authService.Login(loginDto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Dedykowane logowanie do panelu administratora (admin-app).
        /// </summary>
        /// <remarks>
        /// Sprawdza poświadczenia oraz weryfikuje posiadanie roli 'Administrator' (fail-fast).
        /// Wystawia Access Token na 15 minut oraz Refresh Token ze skróconym czasem sesji (4 godziny).
        /// </remarks>
        /// <param name="loginDto">Identyfikator i hasło administratora.</param>
        /// <response code="200">Pomyślne uwierzytelnienie administratora.</response>
        /// <response code="400">Błędne dane logowania lub brak uprawnień administratora.</response>
        /// <response code="429">Zbyt wiele prób logowania.</response>
        [EnableRateLimiting("LoginPolicy")]
        [HttpPost("admin-login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AdminLogin([FromBody] LoginDto loginDto)
        {
            var result = await _authService.AdminLogin(loginDto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Odświeża wygasły token dostępowy (JWT) przy użyciu Refresh Tokena.
        /// </summary>
        /// <remarks>
        /// Realizuje wzorzec Refresh Token Rotation (RTR):
        /// - Sprawdza ważność przesłanego tokena odświeżającego w bazie PostgreSQL.
        /// - Unieważnia stary token odświeżający i wystawia nową parę (Access Token + nowy Refresh Token).
        /// - Implementuje Reuse Detection: próba użycia unieważnionego tokena powoduje natychmiastowe zerwanie wszystkich sesji użytkownika.
        /// </remarks>
        /// <param name="dto">Ciąg znaków aktywnego Refresh Tokena.</param>
        /// <response code="200">Zwraca nową parę tokenów.</response>
        /// <response code="400">Token jest nieprawidłowy, wygasł lub wykryto próbę naruszenia bezpieczeństwa sesji.</response>
        [HttpPost("refresh-token")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
        {
            var result = await _authService.RefreshTokenAsync(dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Unieważnia (odwołuje) wskazany Refresh Token.
        /// </summary>
        /// <remarks>Wywoływany opcjonalnie podczas wylogowania lub zamykania konkretnej sesji urządzenia.</remarks>
        /// <param name="dto">Token do unieważnienia.</param>
        /// <response code="200">Token został oznaczony jako unieważniony.</response>
        [HttpPost("revoke-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequestDto dto)
        {
            var result = await _authService.RevokeTokenAsync(dto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Inicjuje procedurę resetowania zapomnianego hasła.
        /// </summary>
        /// <remarks>
        /// Zabezpieczony przed enumeracją: zawsze zwraca 200 OK.
        /// Jeśli konto istnieje, wysyła wiadomość z linkiem do zresetowania hasła.
        /// </remarks>
        /// <param name="forgotPasswordDto">Adres e-mail konta.</param>
        /// <response code="200">Wiadomość została wysłana, jeśli adres istnieje w systemie.</response>
        [HttpPost("forgot-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Ustawia nowe hasło na podstawie tokena resetującego/aktywacyjnego.
        /// </summary>
        /// <remarks>
        /// Wykorzystywany zarówno przy resecie hasła, jak i przy pierwszym dokończeniu rejestracji z linku.
        /// Opcjonalnie pozwala użytkownikowi zatwierdzić lub zmienić swój login (Username).
        /// Automatycznie oznacza konto jako aktywne (EmailConfirmed = true).
        /// </remarks>
        /// <param name="resetPasswordDto">E-mail, token, nowe hasło oraz opcjonalna nowa nazwa użytkownika.</param>
        /// <response code="200">Hasło zostało pomyślnie zaktualizowane, konto jest gotowe do logowania.</response>
        /// <response code="400">Token wygasł, hasło nie spełnia wymagań lub wybrana nazwa użytkownika jest już zajęta.</response>
        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            var result = await _authService.ResetPasswordAsync(resetPasswordDto);
            return HandleServiceResult(result);
        }

        /// <summary>
        /// Rozpoczyna proces logowania federacyjnego (Google OAuth2).
        /// </summary>
        /// <param name="provider">Dostawca tożsamości (domyślnie "Google").</param>
        /// <param name="returnUrl">Opcjonalny adres powrotny w zaufanej domenie frontendu.</param>
        /// <response code="302">Przekierowanie do strony logowania zewnętrznego dostawcy.</response>
        [HttpGet("external-login")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status302Found)]
        public IActionResult ExternalLogin(string provider = "Google", string? returnUrl = null)
        {
            var frontendOrigin = FrontendOriginResolver.Resolve(_configuration, returnUrl);
            if (frontendOrigin is null)
            {
                return Problem("Brak poprawnego FRONTEND_BASE_URL lub origin nie jest dozwolony.");
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), null, new { returnUrl = frontendOrigin });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            properties.Items["frontendOrigin"] = frontendOrigin;
            return new ChallengeResult(provider, properties);
        }

        /// <summary>
        /// Punkt powrotny (Callback) obsługujący odpowiedź od zewnętrznego dostawcy tożsamości OAuth.
        /// </summary>
        /// <remarks>
        /// Pobiera tożsamość z tokena dostawcy, zakłada konto lokalne w razie potrzeby (z EmailConfirmed = true)
        /// i przekierowuje użytkownika do frontendu z wystawioną parą tokenów JWT i Refresh Token.
        /// </remarks>
        [HttpGet("external-login-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            var frontendUrl = FrontendOriginResolver.Resolve(_configuration, returnUrl);
            if (frontendUrl is null)
            {
                return Problem("Brak poprawnego FRONTEND_BASE_URL lub origin nie jest dozwolony.");
            }

            if (remoteError != null)
            {
                return Redirect($"{frontendUrl}/login?error={WebUtility.UrlEncode(remoteError)}");
            }

            var result = await _authService.HandleExternalLoginAsync();
            if (!result.IsSuccess)
            {
                return Redirect($"{frontendUrl}/login?error={WebUtility.UrlEncode(result.Error.Description)}");
            }

            return Redirect($"{frontendUrl}/login-success?token={result.Data.Token}&refreshToken={result.Data.RefreshToken}");
        }

        /// <summary>
        /// Wylogowuje aktualnie uwierzytelnionego użytkownika i unieważnia sesję serwera.
        /// </summary>
        /// <remarks>Wymaga obecności ważnego tokena Bearer w nagłówku Authorization.</remarks>
        /// <response code="200">Pomyślne wylogowanie.</response>
        /// <response code="401">Brak autoryzacji (użytkownik nie jest zalogowany).</response>
        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            var result = await _authService.LogoutAsync();
            return HandleServiceResult(result);
        }
    }
}
