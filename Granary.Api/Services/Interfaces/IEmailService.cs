using Granary.Api.Models.Dto.Contact;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IEmailService
    {
        Task<ServiceResult> SendContactMessageToAdminAsync(ContactMessageDto message);
        Task<ServiceResult> SendSharedPantryInvitationAsync(string senderName, string recipientEmail, string pantryName, string invitationUrl, bool userExists);
        Task<ServiceResult> SendExpiringProductsAlertAsync(string recipientEmail, string pantryName, int expiringCount, string productsListHtml);
    }
}