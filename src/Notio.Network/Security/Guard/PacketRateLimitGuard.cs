using Notio.Common.Package.Attributes;
using Notio.Network.Configurations;
using System.Collections.Concurrent;

namespace Notio.Network.Security.Guard;

/// <summary>
/// Provides fine-grained rate limiting at the packet level based on endpoint and method identifiers.
/// </summary>
public sealed class PacketRateLimitGuard
{
    private readonly ConcurrentDictionary<(string endpoint, string methodId), RequestLimiter> _methodRateLimiters = new();

    /// <summary>
    /// Checks whether a request to a specific endpoint and method is within the allowed rate limit.
    /// </summary>
    /// <param name="endpoint">The remote endpoint (e.g., IP address).</param>
    /// <param name="methodId">The method identifier used to distinguish packet types.</param>
    /// <param name="attr">The rate limit attribute containing configuration parameters.</param>
    /// <param name="groupAttr">The group rate limit attribute, if applicable.</param>
    /// <returns><c>true</c> if the request is allowed; otherwise, <c>false</c>.</returns>
    public bool Check(string endpoint, string methodId,
        PacketRateLimitAttribute attr, PacketRateGroupAttribute? groupAttr)
    {
        string key = groupAttr?.GroupName ?? methodId;
        RequestLimiter limiter = _methodRateLimiters.GetOrAdd((endpoint, key), _ =>
            new RequestLimiter(new RequestConfig
            {
                MaxAllowedRequests = attr.MaxRequests,
                TimeWindowInMilliseconds = attr.TimeWindowMs,
                LockoutDurationSeconds = attr.LockoutDurationSeconds
            }));

        return limiter.CheckLimit(endpoint);
    }
}
