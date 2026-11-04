using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Profile;
using Granary.Api.Models.Enums;
using Granary.Api.Services.Auth;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Granary.Api.Services
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ProfileService> _logger;
        private readonly GranaryDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly IConfiguration _configuration;

        public ProfileService(
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IEmailService emailService,
            ILogger<ProfileService> logger,
            GranaryDbContext dbContext,
            ICurrentUserService currentUserService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _emailService = emailService;
            _logger = logger;
            _dbContext = dbContext;
            _currentUserService = currentUserService;
            _configuration = configuration;
        }

        public async Task<ServiceResult<UserProfileDto>> GetProfileAsync()
        {
            var userId = _currentUserService.UserId;
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ServiceResult<UserProfileDto>.Failure("Użytkownik nie istnieje.");
            }

            var dto = new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                DarkMode = user.DarkMode,
                Language = user.Language,
                DietaryPreference = user.DietaryPreference
            };

            return ServiceResult<UserProfileDto>.Success(dto);
        }

        public async Task<ServiceResult<UpdateProfileResponseDto>> UpdateProfileAsync(UpdateProfileDto dto)
        {
            var userId = _currentUserService.UserId;
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ServiceResult<UpdateProfileResponseDto>.Failure("Nie znaleziono użytkownika.");
            }

            if (!string.IsNullOrWhiteSpace(dto.Username) && dto.Username.Trim() != user.UserName)
            {
                var setUserNameResult = await _userManager.SetUserNameAsync(user, dto.Username.Trim());
                if (!setUserNameResult.Succeeded)
                {
                    var errors = string.Join(", ", setUserNameResult.Errors.Select(e => e.Description));
                    return ServiceResult<UpdateProfileResponseDto>.Failure($"Błąd zmiany nazwy: {errors}");
                }
            }

            if (dto.Avatar != null && dto.Avatar.Length > 0)
            {
                var previousAvatarUrl = user.AvatarUrl;
                var newAvatarUrl = await _fileStorageService.SaveFileAsync(dto.Avatar, "avatars");
                if (string.IsNullOrWhiteSpace(newAvatarUrl))
                {
                    return ServiceResult<UpdateProfileResponseDto>.Failure("Nie udało się zapisać przesłanego zdjęcia profilowego.");
                }

                user.AvatarUrl = newAvatarUrl;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _fileStorageService.DeleteFile(newAvatarUrl);
                    return ServiceResult<UpdateProfileResponseDto>.Failure("Nie udało się zaktualizować zdjęcia profilowego.");
                }

                if (!string.IsNullOrWhiteSpace(previousAvatarUrl))
                {
                    _fileStorageService.DeleteFile(previousAvatarUrl);
                }
            }

            _logger.LogInformation("Zaktualizowano profil użytkownika {UserId}", userId);

            return ServiceResult<UpdateProfileResponseDto>.Success(new UpdateProfileResponseDto(user.AvatarUrl, user.UserName));
        }

        public async Task<ServiceResult<bool>> UpdateDarkModeAsync(bool darkMode)
        {
            var userId = _currentUserService.UserId;
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return ServiceResult<bool>.Failure("Użytkownik nie istnieje.");

            user.DarkMode = darkMode;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return ServiceResult<bool>.Failure("Nie udało się zaktualizować motywu.");
            }

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<bool>> UpdateLanguageAsync(string language)
        {
            var userId = _currentUserService.UserId;
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return ServiceResult<bool>.Failure("Użytkownik nie istnieje.");

            user.Language = language.Trim();
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return ServiceResult<bool>.Failure("Nie udało się zaktualizować języka.");
            }

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<bool>> UpdateDietaryPreferenceAsync(DietaryPreference dietaryPreference)
        {
            if (!Enum.IsDefined(typeof(DietaryPreference), dietaryPreference))
            {
                return ServiceResult<bool>.Failure("Nieprawidłowa preferencja dietetyczna.");
            }

            var userId = _currentUserService.UserId;
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return ServiceResult<bool>.Failure("Użytkownik nie istnieje.");

            user.DietaryPreference = dietaryPreference;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return ServiceResult<bool>.Failure("Nie udało się zaktualizować preferencji dietetycznej.");
            }

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<bool>> SendInvitationAsync(SendInvitationRequestDto dto)
        {
            var userId = _currentUserService.UserId;
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return ServiceResult<bool>.Failure("Użytkownik nie istnieje.");

            var recipient = await _userManager.FindByEmailAsync(dto.RecipientEmail);
            bool userExists = recipient != null;
            var frontendUrl = FrontendOriginResolver.Resolve(_configuration)
                ?? _configuration["FRONTEND_BASE_URL"]
                ?? "http://localhost:4200";

            var invitationUrl = $"{frontendUrl}/join/{user.Id}";

            var result = await _emailService.SendSharedPantryInvitationAsync(
                senderName: user.UserName ?? "Domownik",
                recipientEmail: dto.RecipientEmail,
                pantryName: dto.PantryName,
                invitationUrl: invitationUrl,
                userExists: userExists
            );

            if (!result.IsSuccess)
            {
                var errorMsg = result.Error?.Description ?? "Wystąpił błąd podczas wysyłania zaproszenia.";
                return ServiceResult<bool>.Failure(errorMsg);
            }

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<bool>> UpdateExpiryNotificationsAsync(bool enabled)
        {
            var userId = _currentUserService.UserId;
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return ServiceResult<bool>.Failure("Nie znaleziono profilu użytkownika.");

            user.ExpiryNotifications = enabled;
            await _dbContext.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<List<NotificationDto>>> GetNotificationsAsync(bool onlyUnread)
        {
            var userId = _currentUserService.UserId;
            var query = _dbContext.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId);

            if (onlyUnread)
            {
                query = query.Where(n => !n.IsRead);
            }

            var list = await query
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    PantryItemId = n.PantryItemId,
                    ProductName = n.ProductName,
                    DaysRemaining = n.DaysRemaining,
                    IsRead = n.IsRead,
                    CreatedAtUtc = n.CreatedAtUtc
                })
                .ToListAsync();

            return ServiceResult<List<NotificationDto>>.Success(list);
        }

        public async Task<ServiceResult<bool>> MarkNotificationAsReadAsync(Guid notificationId)
        {
            var userId = _currentUserService.UserId;
            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return ServiceResult<bool>.Failure("Nie znaleziono powiadomienia.");

            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<bool>> SavePushSubscriptionAsync(PushSubscriptionDto dto)
        {
            var userId = _currentUserService.UserId;

            var existing = await _dbContext.DevicePushSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == dto.Endpoint);

            if (existing is null)
            {
                _dbContext.DevicePushSubscriptions.Add(new DevicePushSubscription
                {
                    UserId = userId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.P256dh,
                    Auth = dto.Auth
                });
            }
            else
            {
                existing.P256dh = dto.P256dh;
                existing.Auth = dto.Auth;
            }

            await _dbContext.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }

        public Task<ServiceResult<object>> GetPushPublicKeyAsync()
        {
            var key = _configuration["VAPID_PUBLIC_KEY"];
            if (string.IsNullOrEmpty(key))
            {
                return Task.FromResult(ServiceResult<object>.Failure("Klucz VAPID nie został skonfigurowany na serwerze."));
            }

            return Task.FromResult(ServiceResult<object>.Success(new { publicKey = key }));
        }
    }
}