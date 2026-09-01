using System.Globalization;
using Granary.Api.Models.Dto;
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

        public EmailService(IEmailSender emailSender, ILogger<EmailService> logger, IConfiguration configuration)
        {
            _emailSender = emailSender;
            _logger = logger;
            _configuration = configuration;
        }

        #region Formularz Kontaktowy

        public async Task<ServiceResult> SendContactMessageToAdminAsync(ContactMessageDto message)
        {
            try
            {
                string subject = "Zapytanie z aplikacji Smart Pantry";

                string adminEmail = _configuration["EmailConfiguration:From"];
                if (string.IsNullOrEmpty(adminEmail))
                {
                    _logger.LogError("Adres email administratora (EmailConfiguration:From) nie jest ustawiony w konfiguracji.");
                    return ServiceResult.Failure(EmailError.ConfigurationError());
                }

                string emailSubject = $"[Smart Pantry] Nowa wiadomość: {subject}";
                string emailBody = $@"
                    <h2>Nowa wiadomość z formularza kontaktowego Smart Pantry</h2>
                    <p><strong>Od:</strong> {message.Name} ({message.Email})</p>
                    <p><strong>Temat:</strong> {subject}</p>
                    <p><strong>Wiadomość:</strong></p>
                    <blockquote style='background-color: #F6F3F2; padding: 12px; border-left: 4px solid #536500;'>
                        {message.Message}
                    </blockquote>
                    <hr>
                    <p>Wiadomość wygenerowana automatycznie przez system Smart Pantry.</p>
                ";

                await _emailSender.SendEmailAsync(adminEmail, emailSubject, emailBody);
                await SendContactConfirmationToUserAsync(message, subject);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wysyłania wiadomości kontaktowej.");
                return ServiceResult.Failure(EmailError.SendFailed());
            }
        }

        private async Task SendContactConfirmationToUserAsync(ContactMessageDto message, string subject)
        {
            try
            {
                string confirmationSubject = "Potwierdzenie otrzymania wiadomości - Smart Pantry";
                string confirmationBody = $@"
                    <h2>Dziękujemy za kontakt!</h2>
                    <p>Otrzymaliśmy Twoją wiadomość i nasz zespół odpowie na nią jak najszybciej.</p>
                    <p><strong>Treść Twojej wiadomości:</strong></p>
                    <blockquote style='background-color: #F6F3F2; padding: 12px; border-left: 4px solid #536500;'>
                        {message.Message}
                    </blockquote>
                    <hr>
                    <p>Z wyrazami szacunku,<br><strong>Zespół Smart Pantry</strong></p>
                ";

                await _emailSender.SendEmailAsync(message.Email, confirmationSubject, confirmationBody);
                _logger.LogInformation("Potwierdzenie wysłania wiadomości zostało wysłane do {UserEmail}.", message.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się wysłać emaila z potwierdzeniem do {UserEmail}.", message.Email);
            }
        }

        #endregion

        #region Zaproszenia do Wspólnej Spiżarni

        public async Task<ServiceResult> SendSharedPantryInvitationAsync(string senderName, string recipientEmail, string pantryName, string invitationUrl, bool userExists)
        {
            try
            {
                string subject = $"{senderName} zaprasza Cię do wspólnej spiżarni '{pantryName}' w Smart Pantry!";
                string templateFileName = userExists
                    ? "SharedPantryInvitationExistingUser.html"
                    : "SharedPantryInvitationNewUser.html";

                string? emailBody = await CreateInvitationBodyAsync(senderName, pantryName, invitationUrl, templateFileName);

                if (emailBody == null)
                {
                    return ServiceResult.Failure("Nie udało się utworzyć treści e-maila z zaproszeniem z powodu błędu szablonu.");
                }

                await _emailSender.SendEmailAsync(recipientEmail, subject, emailBody);

                _logger.LogInformation("Zaproszenie do spiżarni '{PantryName}' zostało wysłane do {RecipientEmail}", pantryName, recipientEmail);
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wysyłania zaproszenia do spiżarni dla {RecipientEmail}", recipientEmail);
                return ServiceResult.Failure("Wystąpił nieoczekiwany błąd podczas wysyłania zaproszenia.");
            }
        }

        private async Task<string?> CreateInvitationBodyAsync(string? senderName, string pantryName, string invitationUrl, string templateFileName)
        {
            string templateDirectory = Path.Combine(AppContext.BaseDirectory, "Templates");
            string templatePath = Path.Combine(templateDirectory, templateFileName);

            string emailTemplateContent;
            try
            {
                emailTemplateContent = await File.ReadAllTextAsync(templatePath);
            }
            catch (DirectoryNotFoundException)
            {
                _logger.LogError("Nie znaleziono folderu szablonów email: {TemplateDirectory}", templateDirectory);
                return null;
            }
            catch (FileNotFoundException)
            {
                _logger.LogError("Nie znaleziono pliku szablonu email: {TemplatePath}", templatePath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas odczytu szablonu email z pliku: {TemplatePath}", templatePath);
                return null;
            }

            emailTemplateContent = emailTemplateContent.Replace("{InviterName}", senderName ?? "Współdomownik");
            emailTemplateContent = emailTemplateContent.Replace("{PantryName}", pantryName);
            emailTemplateContent = emailTemplateContent.Replace("{InvitationUrl}", invitationUrl);

            return emailTemplateContent;
        }

        #endregion

        #region Powiadomienia o Wygasających Produktach (Zero-Waste Alert)

        public async Task<ServiceResult> SendExpiringProductsAlertAsync(string recipientEmail, string pantryName, int expiringCount, string productsListHtml)
        {
            try
            {
                string subject = $"🚨 Smart Pantry: {expiringCount} zbliża się do końca ważności w '{pantryName}'";
                string templateFileName = "ExpiringProductsAlert.html";

                string? emailBody = await CreateExpiringProductsBodyAsync(pantryName, expiringCount, productsListHtml, templateFileName);

                if (emailBody == null)
                {
                    return ServiceResult.Failure("Nie udało się utworzyć treści e-maila z alertem ważności.");
                }

                await _emailSender.SendEmailAsync(recipientEmail, subject, emailBody);
                _logger.LogInformation("Wysłano alert o wygasających produktach ({Count} szt.) do {RecipientEmail}", expiringCount, recipientEmail);
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wysyłania alertu o wygasających produktach do {RecipientEmail}", recipientEmail);
                return ServiceResult.Failure(EmailError.SendFailed());
            }
        }

        private async Task<string?> CreateExpiringProductsBodyAsync(string pantryName, int expiringCount, string productsListHtml, string templateFileName)
        {
            string templateDirectory = Path.Combine(AppContext.BaseDirectory, "Templates");
            string templatePath = Path.Combine(templateDirectory, templateFileName);

            try
            {
                string emailBody = await File.ReadAllTextAsync(templatePath);

                emailBody = emailBody.Replace("{PantryName}", pantryName);
                emailBody = emailBody.Replace("{ExpiringCount}", expiringCount.ToString());
                emailBody = emailBody.Replace("{ProductsListHtml}", productsListHtml);

                return emailBody;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się odczytać szablonu alertu wygasania: {TemplatePath}", templatePath);
                return null;
            }
        }

        #endregion
    }
}