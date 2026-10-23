using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Profile;
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

        public ProfileService(
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IEmailService emailService,
            ILogger<ProfileService> logger,
            GranaryDbContext dbContext)
        {
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _emailService = emailService;
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<ServiceResult<UserProfileDto>> GetProfileAsync(Guid userId)
        {
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
                Language = user.Language
            };

            return ServiceResult<UserProfileDto>.Success(dto);
        }

        public async Task<ServiceResult<UpdateProfileResponseDto>> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
        {
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
                if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
                {
                    _fileStorageService.DeleteFile(user.AvatarUrl);
                }

                var newAvatarUrl = await _fileStorageService.SaveFileAsync(dto.Avatar, "avatars");
                user.AvatarUrl = newAvatarUrl;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return ServiceResult<UpdateProfileResponseDto>.Failure("Nie udało się zaktualizować zdjęcia profilowego.");
                }
            }

            _logger.LogInformation("Zaktualizowano profil użytkownika {UserId}", userId);

            return ServiceResult<UpdateProfileResponseDto>.Success(new UpdateProfileResponseDto(user.AvatarUrl, user.UserName));
        }

        public async Task<ServiceResult<bool>> UpdateDarkModeAsync(Guid userId, bool darkMode)
        {
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

        public async Task<ServiceResult<bool>> UpdateLanguageAsync(Guid userId, string language)
        {
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

        public async Task<ServiceResult<bool>> SendInvitationAsync(Guid userId, SendInvitationRequestDto dto, string hostUrl)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return ServiceResult<bool>.Failure("Użytkownik nie istnieje.");

            var recipient = await _userManager.FindByEmailAsync(dto.RecipientEmail);
            bool userExists = recipient != null;

            var invitationUrl = $"{hostUrl}/join/{user.Id}";

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


        public async Task<ServiceResult<bool>> UpdateExpiryNotificationsAsync(Guid userId, bool enabled)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return ServiceResult<bool>.Failure("Nie znaleziono profilu użytkownika.");

            user.ExpiryNotifications = enabled;
            await _dbContext.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<List<NotificationDto>>> GetNotificationsAsync(Guid userId, bool onlyUnread)
        {
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

        public async Task<ServiceResult<bool>> MarkNotificationAsReadAsync(Guid userId, Guid notificationId)
        {
            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return ServiceResult<bool>.Failure("Nie znaleziono powiadomienia.");

            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();

            return ServiceResult<bool>.Success(true);
        }
    }
}