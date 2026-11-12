using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Auth;
using Granary.Api.Models.Settings;
using Granary.Api.Services.Errors;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Granary.Api.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly JwtSettings _jwtSettings;
        private readonly GranaryDbContext _context;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            ILogger<AuthService> logger,
            IConfiguration configuration,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            IOptions<JwtSettings> jwtOptions,
            GranaryDbContext context)
        {
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
            _signInManager = signInManager;
            _emailService = emailService;
            _jwtSettings = jwtOptions.Value;
            _context = context;
        }

        public async Task<ServiceResult> RegisterAsync(RegisterDto registerDto)
        {
            if (string.IsNullOrWhiteSpace(registerDto.Email))
            {
                return ServiceResult.Failure(AuthErrors.EmailRequired());
            }

            var cleanEmail = registerDto.Email.Trim().ToLowerInvariant();

            var existingUser = await _userManager.FindByEmailAsync(cleanEmail);
            if (existingUser != null)
            {
                if (!existingUser.EmailConfirmed)
                {
                    var resendToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                    var resendUrl = FrontendOriginResolver.BuildAccountSetupUrl(_configuration, resendToken, existingUser.Email!, isAdmin: false);
              
                    await _emailService.SendAccountActivationEmailAsync(existingUser.Email!, existingUser.UserName ?? "Użytkowniku", resendUrl);
                }
                return ServiceResult.Success();
            }

            var baseUsername = !string.IsNullOrWhiteSpace(registerDto.Username)
                ? registerDto.Username.Trim()
                : cleanEmail.Split('@')[0];

            var uniqueUsername = baseUsername;
            var counter = 1;
            while (await _userManager.FindByNameAsync(uniqueUsername) != null)
            {
                uniqueUsername = $"{baseUsername}{counter++}";
            }

            var newUser = new ApplicationUser
            {
                Email = cleanEmail,
                UserName = uniqueUsername,
                EmailConfirmed = false,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var createResult = await _userManager.CreateAsync(newUser);
            if (!createResult.Succeeded)
            {
                var details = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return ServiceResult.Failure(AuthErrors.UserCreationFailed(details));
            }

            await _userManager.AddToRoleAsync(newUser, "User");

            var token = await _userManager.GeneratePasswordResetTokenAsync(newUser);
            var setupUrl = FrontendOriginResolver.BuildAccountSetupUrl(_configuration, token, newUser.Email!, isAdmin: false);

            await _emailService.SendAccountActivationEmailAsync(newUser.Email!, newUser.UserName ?? "Użytkowniku", setupUrl);

            _logger.LogInformation("Zarejestrowano użytkownika {Email}. Wysłano maila powitalnego.", cleanEmail);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ConfirmEmailAsync(ConfirmEmailDto confirmEmailDto)
        {
            if (string.IsNullOrWhiteSpace(confirmEmailDto.Email) || string.IsNullOrWhiteSpace(confirmEmailDto.Token))
            {
                return ServiceResult.Failure("Nieprawidłowe parametry aktywacji konta.");
            }

            var user = await _userManager.FindByEmailAsync(confirmEmailDto.Email.Trim().ToLowerInvariant());
            if (user == null)
            {
                return ServiceResult.Failure("Nie znaleziono użytkownika lub link jest nieprawidłowy.");
            }

            if (user.EmailConfirmed)
            {
                return ServiceResult.Success();
            }

            var result = await _userManager.ConfirmEmailAsync(user, confirmEmailDto.Token);
            if (!result.Succeeded)
            {
                return ServiceResult.Failure("Link aktywacyjny jest nieprawidłowy lub wygasł. Poproś o nowy.");
            }

            _logger.LogInformation("Konto użytkownika {Email} zostało aktywowane.", user.Email);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult<LoginResponse>> Login(LoginDto loginDto)
        {
            var user = await FindByIdentifierAsync(loginDto.Identifier);
            if (user == null)
            {
                return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
            }

            if (!user.EmailConfirmed)
            {
                return ServiceResult<LoginResponse>.Failure("Twoje konto nie zostało jeszcze aktywowane. Sprawdź swoją skrzynkę e-mail.");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: true);
            if (result.IsLockedOut)
            {
                return ServiceResult<LoginResponse>.Failure("Konto zostało tymczasowo zablokowane. Spróbuj za 15 minut.");
            }

            if (!result.Succeeded)
            {
                return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
            }

            var accessToken = await GenerateJwtTokenAsync(user, TimeSpan.FromMinutes(15));
            var refreshToken = await CreateAndSaveRefreshTokenAsync(user.Id, TimeSpan.FromDays(14));

            return ServiceResult<LoginResponse>.Success(new LoginResponse(accessToken, refreshToken.Token));
        }

        public async Task<ServiceResult<LoginResponse>> AdminLogin(LoginDto loginDto)
        {
            var user = await FindByIdentifierAsync(loginDto.Identifier);
            if (user == null)
            {
                return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
            if (!isAdmin)
            {
                _logger.LogWarning("Odmowa dostępu do panelu administratora dla {Identifier}.", loginDto.Identifier);
                return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
            }

            if (!user.EmailConfirmed)
            {
                return ServiceResult<LoginResponse>.Failure("Konto administratora nie zostało aktywowane.");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: true);
            if (result.IsLockedOut)
            {
                return ServiceResult<LoginResponse>.Failure("Konto administratora jest tymczasowo zablokowane. Spróbuj za 15 minut.");
            }

            if (!result.Succeeded)
            {
                return ServiceResult<LoginResponse>.Failure(AuthErrors.InvalidCredentials());
            }

            var accessToken = await GenerateJwtTokenAsync(user, TimeSpan.FromMinutes(15));
            var refreshToken = await CreateAndSaveRefreshTokenAsync(user.Id, TimeSpan.FromHours(4));

            _logger.LogInformation("Administrator {Email} zalogował się do panelu.", user.Email);
            return ServiceResult<LoginResponse>.Success(new LoginResponse(accessToken, refreshToken.Token));
        }

        public async Task<ServiceResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            {
                return ServiceResult<LoginResponse>.Failure("Brak tokena odświeżającego.");
            }

            var existingToken = await _context.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

            if (existingToken == null)
            {
                return ServiceResult<LoginResponse>.Failure("Nieprawidłowy token odświeżający.");
            }

            if (existingToken.IsRevoked)
            {
                _logger.LogWarning("Wykryto próbę ponownego użycia unieważnionego Refresh Tokena! User ID: {UserId}. Unieważniam wszystkie sesje użytkownika.", existingToken.UserId);

                var allUserTokens = await _context.RefreshTokens
                    .Where(r => r.UserId == existingToken.UserId && !r.IsRevoked)
                    .ToListAsync();

                foreach (var t in allUserTokens)
                {
                    t.IsRevoked = true;
                    t.RevokedDate = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();

                return ServiceResult<LoginResponse>.Failure("Wykryto naruszenie bezpieczeństwa sesji. Zaloguj się ponownie.");
            }

            if (existingToken.ExpiryDate < DateTime.UtcNow)
            {
                existingToken.IsRevoked = true;
                existingToken.RevokedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return ServiceResult<LoginResponse>.Failure("Sesja wygasła. Zaloguj się ponownie.");
            }

            var user = existingToken.User;
            if (user == null || (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow))
            {
                return ServiceResult<LoginResponse>.Failure("Konto użytkownika jest zablokowane.");
            }

            existingToken.IsRevoked = true;
            existingToken.RevokedDate = DateTime.UtcNow;

            var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
            var refreshLifetime = isAdmin ? TimeSpan.FromHours(4) : TimeSpan.FromDays(14);

            var newAccessToken = await GenerateJwtTokenAsync(user, TimeSpan.FromMinutes(15));
            var newRefreshToken = await CreateAndSaveRefreshTokenAsync(user.Id, refreshLifetime);

            return ServiceResult<LoginResponse>.Success(new LoginResponse(newAccessToken, newRefreshToken.Token));
        }

        public async Task<ServiceResult> RevokeTokenAsync(RevokeTokenRequestDto dto)
        {
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);
            if (token != null && !token.IsRevoked)
            {
                token.IsRevoked = true;
                token.RevokedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return ServiceResult.Success();
        }

        private async Task<RefreshToken> CreateAndSaveRefreshTokenAsync(Guid userId, TimeSpan lifetime)
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var tokenString = Convert.ToBase64String(randomBytes);

            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = tokenString,
                UserId = userId,
                CreationDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.Add(lifetime),
                IsRevoked = false
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        private async Task<ApplicationUser?> FindByIdentifierAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return null;

            var clean = identifier.Trim();
            if (clean.Contains('@'))
            {
                return await _userManager.FindByEmailAsync(clean.ToLowerInvariant());
            }

            return await _userManager.FindByNameAsync(clean);
        }

        private async Task<string> GenerateJwtTokenAsync(ApplicationUser user, TimeSpan lifetime)
        {
            var userIdString = user.Id.ToString();

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userIdString),
                new Claim(ClaimTypes.NameIdentifier, userIdString),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
            };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }

            var secret = _configuration["JWT__Secret"] ?? _configuration["JWT:Secret"] ?? _jwtSettings.Secret;
            var issuer = _configuration["JWT__Issuer"] ?? _configuration["JWT:Issuer"] ?? _jwtSettings.Issuer;
            var audience = _configuration["JWT__Audience"] ?? _configuration["JWT:Audience"] ?? _jwtSettings.Audience;

            if (string.IsNullOrEmpty(secret))
                throw new InvalidOperationException("Brak klucza JWT Secret w konfiguracji.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.Add(lifetime);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
        {
            var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
            if (user == null) return ServiceResult.Success();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = FrontendOriginResolver.BuildResetPasswordUrl(_configuration, token, user.Email!, forgotPasswordDto.FrontendOrigin);

            await _emailService.SendPasswordResetEmailAsync(user.Email!, user.UserName ?? "użytkowniku", callbackUrl);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
        {
            var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
            if (user == null)
            {
                return ServiceResult.Failure(CommonErrors.BadRequest("Nieudana próba resetu hasła."));
            }

            if (!string.IsNullOrWhiteSpace(resetPasswordDto.Username))
            {
                var cleanUsername = resetPasswordDto.Username.Trim();
                if (!string.Equals(user.UserName, cleanUsername, StringComparison.OrdinalIgnoreCase))
                {
                    var userWithSameName = await _userManager.FindByNameAsync(cleanUsername);
                    if (userWithSameName != null && userWithSameName.Id != user.Id)
                    {
                        return ServiceResult.Failure("Podana nazwa użytkownika jest już zajęta. Wybierz inną.");
                    }

                    user.UserName = cleanUsername;
                }
            }

            var result = await _userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.NewPassword);
            if (result.Succeeded)
            {
                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                }

                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Hasło i profil dla {Email} zostały pomyślnie zaktualizowane.", user.Email);
                return ServiceResult.Success();
            }

            var errorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult.Failure(errorMessage);
        }

        public async Task<ServiceResult<LoginResponse>> HandleExternalLoginAsync()
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogWarning("Brak informacji od zewnętrznego dostawcy OAuth.");
                return ServiceResult<LoginResponse>.Failure(AuthErrors.ExternalAuthFailed("Unknown"));
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                var token = await GenerateJwtTokenAsync(user!, TimeSpan.FromMinutes(15));
                var refreshToken = await CreateAndSaveRefreshTokenAsync(user!.Id, TimeSpan.FromDays(14));
                return ServiceResult<LoginResponse>.Success(new LoginResponse(token, refreshToken.Token));
            }
            else
            {
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(email))
                {
                    return ServiceResult<LoginResponse>.Failure(AuthErrors.EmailRequired());
                }

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true
                    };

                    var createUserResult = await _userManager.CreateAsync(user);
                    if (!createUserResult.Succeeded)
                    {
                        return ServiceResult<LoginResponse>.Failure(AuthErrors.UserCreationFailed());
                    }

                    await _userManager.AddToRoleAsync(user, "User");
                }

                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (!addLoginResult.Succeeded)
                {
                    return ServiceResult<LoginResponse>.Failure(AuthErrors.AccountLinkingFailed());
                }

                var token = await GenerateJwtTokenAsync(user, TimeSpan.FromMinutes(15));
                var refreshToken = await CreateAndSaveRefreshTokenAsync(user.Id, TimeSpan.FromDays(14));
                return ServiceResult<LoginResponse>.Success(new LoginResponse(token, refreshToken.Token));
            }
        }

        public async Task<ServiceResult> LogoutAsync()
        {
            await _signInManager.SignOutAsync();
            return ServiceResult.Success();
        }
    }
}
