using Nalix.Common.Package.Attributes;
using Nalix.Common.Security;
using Nalix.Network.Configurations;
using System.Collections.Concurrent;

namespace Nalix.Network.Security.Guard;

/// <summary>
/// Provides fine-grained rate limiting at the packet level based on endpoint and method identifiers.
/// </summary>
public sealed class PacketRateLimitGuard
{
    private readonly ConcurrentDictionary<(string endpoint, RequestLimitType type), RequestLimiter> _methodRateLimiters = [];

    /// <summary>
    /// Checks whether a request to a specific endpoint and method is within the allowed rate limit.
    /// </summary>
    /// <param name="endpoint">The remote endpoint (e.g., IP address).</param>
    /// <param name="attr">The rate limit attribute containing configuration parameters.</param>
    /// <returns><c>true</c> if the request is allowed; otherwise, <c>false</c>.</returns>
    public bool Check(string endpoint, PacketRateLimitAttribute attr)
        => _methodRateLimiters.GetOrAdd(
            (endpoint, attr.Level), _ => new RequestLimiter(new RateLimitConfig(attr.Level))
        ).CheckLimit(endpoint);
}
