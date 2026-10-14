using System.Net;
using Granary.Api.Models.Dto.Contact;
using Granary.Api.Services.Errors;
using Granary.Api.Services.Interfaces;
using Granary.Api.Services.Results;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Granary.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(
            IEmailSender emailSender,
            ILogger<EmailService> logger,
            IConfiguration configuration)
        {
            _emailSender = emailSender;
            _logger = logger;
            _configuration = configuration;
        }

        #region Formularz Kontaktowy (Wiadomość do Administratora)

        public async Task<ServiceResult> SendContactMessageToAdminAsync(ContactMessageDto message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message.Email) || string.IsNullOrWhiteSpace(message.Message))
                {
                    return ServiceResult.Failure("Adres e-mail oraz treść wiadomości są wymagane.");
                }

                string adminEmail = _configuration["EmailConfiguration:AdminEmail"]
                                 ?? _configuration["EmailConfiguration:SupportEmail"]
                                 ?? _configuration["EmailConfiguration:From"]
                                 ?? string.Empty;

                if (string.IsNullOrEmpty(adminEmail))
                {
                    _logger.LogError("Adres email administratora (AdminEmail/From) nie został skonfigurowany w appsettings.json.");
                    return ServiceResult.Failure(EmailError.ConfigurationError());
                }

                string safeName = WebUtility.HtmlEncode(message.Name?.Trim() ?? "Użytkownik");
                string safeEmail = WebUtility.HtmlEncode(message.Email.Trim());
                string safeSubject = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(message.Subject) ? "Zapytanie z Centrum Pomocy" : message.Subject.Trim());
                string safeMessage = WebUtility.HtmlEncode(message.Message.Trim()).Replace("\n", "<br/>");

                string emailSubject = $"[Smart Pantry Wsparcie] {safeSubject}";
                string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; color: #1B1C18; line-height: 1.6; max-width: 600px;'>
                        <h2 style='color: #536500; border-bottom: 2px solid #536500; padding-bottom: 8px;'>Nowa wiadomość z Centrum Pomocy</h2>
                        <p><strong>Użytkownik:</strong> {safeName}</p>
                        <p><strong>Adres zwrotny:</strong> <a href='mailto:{safeEmail}' style='color: #536500;'>{safeEmail}</a></p>
                        <p><strong>Wybrany temat:</strong> {safeSubject}</p>
                        <p><strong>Treść zgłoszenia:</strong></p>
                        <div style='background-color: #F6F3F2; padding: 16px; border-left: 4px solid #536500; border-radius: 6px; margin: 12px 0;'>
                            {safeMessage}
                        </div>
                        <hr style='border: none; border-top: 1px solid #E1E3D5; margin: 20px 0;'>
                        <p style='font-size: 11px; color: #757962;'>Wiadomość wysłana z poziomu profilu użytkownika w aplikacji Smart Pantry.</p>
                    </div>
                ";

                await _emailSender.SendEmailAsync(adminEmail, emailSubject, emailBody);
                _logger.LogInformation("Przesłano wiadomość kontaktową od {UserEmail} do administratora ({AdminEmail}).", message.Email, adminEmail);

                await SendContactConfirmationToUserAsync(message, safeSubject, safeMessage);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wystąpił błąd podczas wysyłania wiadomości kontaktowej.");
                return ServiceResult.Failure(EmailError.SendFailed());
            }
        }

        private async Task SendContactConfirmationToUserAsync(ContactMessageDto message, string safeSubject, string safeMessageHtml)
        {
            try
            {
                string confirmationSubject = "Potwierdzenie otrzymania wiadomości – Smart Pantry";
                string confirmationBody = $@"
                    <div style='font-family: Arial, sans-serif; color: #1B1C18; line-height: 1.6; max-width: 600px;'>
                        <h2 style='color: #536500;'>Dziękujemy za kontakt!</h2>
                        <p>Witaj <strong>{WebUtility.HtmlEncode(message.Name ?? "Użytkowniku")}</strong>,</p>
                        <p>Otrzymaliśmy Twoją wiadomość w sprawie: <em>{safeSubject}</em>. Nasz zespół odpowie na nią bezpośrednio na ten adres e-mail.</p>
                        <div style='background-color: #F6F3F2; padding: 14px; border-left: 4px solid #536500; border-radius: 4px; margin: 16px 0; font-size: 13px;'>
                            {safeMessageHtml}
                        </div>
                        <hr style='border: none; border-top: 1px solid #E1E3D5; margin: 20px 0;'>
                        <p style='color: #536500; font-weight: bold;'>Zespół Smart Pantry</p>
                    </div>
                ";

                await _emailSender.SendEmailAsync(message.Email, confirmationSubject, confirmationBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się wysłać automatycznego potwierdzenia do użytkownika {UserEmail}.", message.Email);
            }
        }

        #endregion

        #region Zaproszenia do Wspólnej Spiżarni

        public async Task<ServiceResult> SendSharedPantryInvitationAsync(
            string senderName,
            string recipientEmail,
            string pantryName,
            string invitationUrl,
            bool userExists)
        {
            try
            {
                string subject = $"{senderName} zaprasza Cię do wspólnej spiżarni '{pantryName}'!";
                string templateFileName = userExists
                    ? "SharedPantryInvitationExistingUser.html"
                    : "SharedPantryInvitationNewUser.html";

                string? emailBody = await CreateInvitationBodyAsync(senderName, pantryName, invitationUrl, templateFileName);

                if (emailBody == null)
                {
                    return ServiceResult.Failure("Błąd szablonu zaproszenia.");
                }

                await _emailSender.SendEmailAsync(recipientEmail, subject, emailBody);
                _logger.LogInformation("Zaproszenie do spiżarni '{PantryName}' wysłane do {RecipientEmail}", pantryName, recipientEmail);
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wysyłania zaproszenia do {RecipientEmail}", recipientEmail);
                return ServiceResult.Failure("Wystąpił błąd podczas wysyłania zaproszenia.");
            }
        }

        private async Task<string?> CreateInvitationBodyAsync(string? senderName, string pantryName, string invitationUrl, string templateFileName)
        {
            string templatePath = ResolveTemplatePath(templateFileName);
            if (!File.Exists(templatePath))
            {
                _logger.LogError("Nie znaleziono pliku szablonu email: {TemplatePath}", templatePath);
                return null;
            }

            string content = await File.ReadAllTextAsync(templatePath);
            content = content.Replace("{InviterName}", WebUtility.HtmlEncode(senderName ?? "Współdomownik"));
            content = content.Replace("{PantryName}", WebUtility.HtmlEncode(pantryName));
            content = content.Replace("{InvitationUrl}", invitationUrl);

            return content;
        }

        #endregion

        #region Powiadomienia o Wygasających Produktach (Zero-Waste Alert)

        public async Task<ServiceResult> SendExpiringProductsAlertAsync(
            string recipientEmail,
            string pantryName,
            int expiringCount,
            string productsListHtml)
        {
            try
            {
                string subject = $"🚨 Smart Pantry: {expiringCount} produktów kończy ważność w '{pantryName}'";
                string templateFileName = "ExpiringProductsAlert.html";

                string? emailBody = await CreateExpiringProductsBodyAsync(pantryName, expiringCount, productsListHtml, templateFileName);

                if (emailBody == null)
                {
                    return ServiceResult.Failure("Nie udało się utworzyć treści alertu.");
                }

                await _emailSender.SendEmailAsync(recipientEmail, subject, emailBody);
                _logger.LogInformation("Wysłano alert o wygasających produktach ({Count}) do {RecipientEmail}", expiringCount, recipientEmail);
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wysyłania alertu wygasania do {RecipientEmail}", recipientEmail);
                return ServiceResult.Failure(EmailError.SendFailed());
            }
        }

        private async Task<string?> CreateExpiringProductsBodyAsync(string pantryName, int expiringCount, string productsListHtml, string templateFileName)
        {
            string templatePath = ResolveTemplatePath(templateFileName);
            if (!File.Exists(templatePath))
            {
                _logger.LogError("Nie znaleziono szablonu alertu wygasania: {TemplatePath}", templatePath);
                return null;
            }

            string emailBody = await File.ReadAllTextAsync(templatePath);
            emailBody = emailBody.Replace("{PantryName}", WebUtility.HtmlEncode(pantryName));
            emailBody = emailBody.Replace("{ExpiringCount}", expiringCount.ToString());
            emailBody = emailBody.Replace("{ProductsListHtml}", productsListHtml);

            return emailBody;
        }

        #endregion

        private static string ResolveTemplatePath(string templateFileName)
        {
            string primaryPath = Path.Combine(AppContext.BaseDirectory, "Templates", templateFileName);
            if (File.Exists(primaryPath)) return primaryPath;

            return Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFileName);
        }
    }
}