using System.Net;

namespace Granary.Api.Services
{
    public static class PasswordLinkBuilder
    {
        private const string DefaultFrontendUrl = "http://localhost:3000";

        /// <summary>
        /// Buduje adres strony ustawiania hasła po stronie aplikacji klienckiej (FRONTEND_BASE_URL).
        /// </summary>
        public static string BuildResetUrl(IConfiguration configuration, string token, string email)
        {
            var baseUrl = configuration["FRONTEND_BASE_URL"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = DefaultFrontendUrl;
            }

            return $"{baseUrl.TrimEnd('/')}/reset-password?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(email)}";
        }
    }
}
