namespace Notio.Common.Firewall;

/// <summary>
/// Interface for rate limiting functionality.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Checks if the rate limit has been exceeded for a given key.
    /// </summary>
    /// <param name="key">The key to check the rate limit for.</param>
    /// <returns>True if the rate limit has not been exceeded; otherwise, false.</returns>
    bool CheckLimit(string key);
}