using Granary.Api.Models.DatabaseModels;
using Granary.Api.Models.Dto.Profile;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Granary.Api.Services.Storage;
using Microsoft.AspNetCore.Identity;

namespace Granary.Api.Services
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IEmailService emailService,
            ILogger<ProfileService> logger)
        {
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _emailService = emailService;
            _logger = logger;
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

            if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                user.UserName = dto.Username.Trim();
            }

            if (dto.Avatar != null && dto.Avatar.Length > 0)
            {
                if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
                {
                    _fileStorageService.DeleteFile(user.AvatarUrl);
                }

                var newAvatarUrl = await _fileStorageService.SaveFileAsync(dto.Avatar, "avatars");
                user.AvatarUrl = newAvatarUrl;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Błąd aktualizacji profilu {UserId}: {Errors}", userId, errors);
                return ServiceResult<UpdateProfileResponseDto>.Failure("Nie udało się zaktualizować profilu.");
            }

            _logger.LogInformation("Zaktualizowano profil użytkownika {UserId}", userId);

            return ServiceResult<UpdateProfileResponseDto>.Success(new UpdateProfileResponseDto(user.AvatarUrl));
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
    }
}