using Granary.Api.Models.Dto.Contact;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IEmailService
    {
        Task<ServiceResult> SendContactMessageToAdminAsync(ContactMessageDto message);
        Task<ServiceResult> SendSharedPantryInvitationAsync(string senderName, string recipientEmail, string pantryName, string invitationUrl, bool userExists);
        Task<ServiceResult> SendAccountSetupAsync(string recipientEmail, string userName, string setupUrl);
        Task<ServiceResult> SendExpiringProductsAlertAsync(string recipientEmail, string pantryName, int expiringCount, string productsListHtml);
        Task<ServiceResult> SendPasswordResetEmailAsync(string recipientEmail, string userName, string resetUrl);
        Task<ServiceResult> SendAccountActivationEmailAsync(string recipientEmail, string userName, string activationUrl);
    }
}