using Granary.Api.Models.Dto.Profile;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IProfileService
    {
        Task<ServiceResult<UserProfileDto>> GetProfileAsync(Guid userId);
        Task<ServiceResult<UpdateProfileResponseDto>> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
        Task<ServiceResult<bool>> UpdateDarkModeAsync(Guid userId, bool darkMode);
        Task<ServiceResult<bool>> UpdateLanguageAsync(Guid userId, string language);
        Task<ServiceResult<bool>> SendInvitationAsync(Guid userId, SendInvitationRequestDto dto, string hostUrl);
        Task<ServiceResult<bool>> UpdateExpiryNotificationsAsync(Guid userId, bool enabled);
        Task<ServiceResult<List<NotificationDto>>> GetNotificationsAsync(Guid userId, bool onlyUnread);
        Task<ServiceResult<bool>> MarkNotificationAsReadAsync(Guid userId, Guid notificationId);
    }
}