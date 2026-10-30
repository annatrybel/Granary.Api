using System.Linq.Expressions;
using Granary.Api.Models;
using Granary.Api.Models.DatabaseModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Granary.Api.Extensions;

public static class LanguageCacheExtensions
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Zwraca identyfikator języka z pamięci podręcznej. Lista języków prawie się nie zmienia,
    /// więc unikamy dodatkowego zapytania do bazy przy każdym żądaniu.
    /// </summary>
    public static async Task<Guid> GetLanguageIdAsync(
        this IMemoryCache cache,
        GranaryDbContext context,
        string cacheKey,
        Expression<Func<Language, bool>> predicate)
    {
        if (cache.TryGetValue(cacheKey, out Guid cached))
        {
            return cached;
        }

        var language = await context.Languages.AsNoTracking().FirstOrDefaultAsync(predicate);
        if (language == null)
        {
            return Guid.Empty;
        }

        cache.Set(cacheKey, language.Id, CacheDuration);
        return language.Id;
    }
}
