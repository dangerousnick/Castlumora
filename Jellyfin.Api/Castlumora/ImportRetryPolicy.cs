using System;

namespace Jellyfin.Api.Castlumora;

/// <summary>Opt-in transient backoff within the existing three-attempt job budget.</summary>
internal static class ImportRetryPolicy
{
    internal static TimeSpan? Delay(string? code, int attempts)
    {
        if (attempts is < 1 or >= 3)
        {
            return null;
        }

        return code switch
        {
            "sourceNetwork" => TimeSpan.FromSeconds(15 * attempts),
            "sourceRateLimited" => TimeSpan.FromMinutes(attempts),
            _ => null
        };
    }
}
