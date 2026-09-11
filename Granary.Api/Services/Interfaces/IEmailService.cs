using Granary.Api.Models.Dto.Contact;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IEmailService
    {
        Task<ServiceResult> SendContactMessageToAdminAsync(ContactMessageDto message);
        Task SendContactConfirmationToUserAsync(ContactMessageDto message, string subject);
        Task<ServiceResult> SendSharedPantryInvitationAsync(string senderName, string recipientEmail, string pantryName, string invitationUrl, bool userExists);
        Task<string?> CreateInvitationBodyAsync(string? senderName, string pantryName, string invitationUrl, string templateFileName);
        Task<ServiceResult> SendExpiringProductsAlertAsync(string recipientEmail, string pantryName, int expiringCount, string productsListHtml);
        Task<string?> CreateExpiringProductsBodyAsync(string pantryName, int expiringCount, string productsListHtml, string templateFileName);
    }
}