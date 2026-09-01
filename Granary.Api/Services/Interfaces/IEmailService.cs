using Granary.Api.Models.Dto;
using Granary.Api.Services.Results;

namespace Granary.Api.Services.Interfaces
{
    public interface IEmailService
    {
        Task<ServiceResult> SendContactMessageToAdminAsync(ContactMessageDto message);
        Task<ServiceResult> SendBudgetInvitationAsync(string senderName, string recipientEmail, string budgetName, string invitationUrl, bool userExists);
        Task<ServiceResult> SendRecurrentExpenseSuccessNotificationAsync(string recipientEmail, string budgetName, string transactionTitle, decimal amount);
        Task<ServiceResult> SendRecurrentExpenseFailedNotificationAsync(string recipientEmail, string budgetName, string transactionTitle, decimal amount, string reason);
    }
}