using System.Net;

namespace Granary.Api.Services.Auth;

public static class FrontendOriginResolver
{
    /// <summary>
    /// Zwraca listę wszystkich zaufanych domen frontendowych (User App + Admin App).
    /// </summary>
    public static string[] GetAllowedOrigins(IConfiguration configuration)
    {
        var userOrigin = ExtractOrigin(configuration["FRONTEND_BASE_URL"]);
        var adminOrigin = ExtractOrigin(configuration["ADMIN_BASE_URL"]);

        return new[] { userOrigin, adminOrigin }
            .Where(url => !string.IsNullOrEmpty(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    /// <summary>
    /// Sprawdza, czy podany kandydat (np. returnUrl lub origin z formularza) należy do zaufanych domen.
    /// Jeśli tak, zwraca dopasowaną domenę bazową. Jeśli nie podano kandydata, zwraca domyślny FRONTEND_BASE_URL.
    /// </summary>
    public static string? Resolve(IConfiguration configuration, string? candidate = null)
    {
        var allowedOrigins = GetAllowedOrigins(configuration);
        if (allowedOrigins.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var candidateOrigin = ExtractOrigin(candidate);
            if (candidateOrigin is not null)
            {
                var matched = allowedOrigins.FirstOrDefault(o =>
                    string.Equals(o, candidateOrigin, StringComparison.OrdinalIgnoreCase));

                if (matched is not null)
                {
                    return matched;
                }
            }

            // Jeśli podano kandydata, ale z obcej/nieznanej domeny -> blokujemy (Open Redirect protection)
            return null;
        }

        // Domyślnie zwracamy domenę użytkownika (pierwszą skonfigurowaną)
        return allowedOrigins[0];
    }

    /// <summary>
    /// Bezpiecznie wyciąga sam protokół + host (Origin), ignorując podstrony, query i fragmenty.
    /// Np. "https://granary-app.vercel.app/przepisy?lang=pl" -> "https://granary-app.vercel.app"
    /// </summary>
    public static string? ExtractOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            string.IsNullOrEmpty(uri.UserInfo))
        {
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        return null;
    }


    // W pliku FrontendOriginResolver.cs:
    public static string BuildResetPasswordUrl(IConfiguration configuration, string token, string email, string? frontendOrigin = null)
    {
        var baseUrl = Resolve(configuration, frontendOrigin)
            ?? throw new InvalidOperationException("Brak poprawnego FRONTEND_BASE_URL w konfiguracji.");

        return $"{baseUrl}/reset-password?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(email)}";
    }
}