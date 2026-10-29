using Granary.Api.Models.Dto.Profile;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IProfileService
    {
        Task<ServiceResult<UserProfileDto>> GetProfileAsync();
        Task<ServiceResult<UpdateProfileResponseDto>> UpdateProfileAsync(UpdateProfileDto dto);
        Task<ServiceResult<bool>> UpdateDarkModeAsync(bool darkMode);
        Task<ServiceResult<bool>> UpdateLanguageAsync(string language);
        Task<ServiceResult<bool>> SendInvitationAsync(SendInvitationRequestDto dto, string hostUrl);
        Task<ServiceResult<bool>> UpdateExpiryNotificationsAsync(bool enabled);
        Task<ServiceResult<List<NotificationDto>>> GetNotificationsAsync(bool onlyUnread);
        Task<ServiceResult<bool>> MarkNotificationAsReadAsync(Guid notificationId);
    }
}