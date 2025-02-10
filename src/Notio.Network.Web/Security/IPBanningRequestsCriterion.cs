using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Network.Web.Security;

/// <summary>
/// Represents a maximun requests per second criterion for <see cref="IPBanningModule"/>.
/// </summary>
/// <seealso cref="IIPBanningCriterion" />
public class IPBanningRequestsCriterion : IIPBanningCriterion
{
    /// <summary>
    /// The default maximum request per second.
    /// </summary>
    public const int DefaultMaxRequestsPerSecond = 50;

    private static readonly ConcurrentDictionary<IPAddress, ConcurrentBag<long>> Requests = new();

    private readonly int _maxRequestsPerSecond;

    private bool _disposed;

    internal IPBanningRequestsCriterion(int maxRequestsPerSecond)
    {
        _maxRequestsPerSecond = maxRequestsPerSecond;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="IPBanningRequestsCriterion"/> class.
    /// </summary>
    ~IPBanningRequestsCriterion()
    {
        Dispose(false);
    }

    /// <inheritdoc />
    public Task<bool> ValidateIPAddress(IPAddress address)
    {
        Requests.GetOrAdd(address, []).Add(DateTime.Now.Ticks);

        long lastSecond = DateTime.Now.AddSeconds(-1).Ticks;
        long lastMinute = DateTime.Now.AddMinutes(-1).Ticks;

        bool shouldBan = Requests.TryGetValue(address, out ConcurrentBag<long>? attempts) &&
            (attempts.Count(x => x >= lastSecond) >= _maxRequestsPerSecond ||
             attempts.Count(x => x >= lastMinute) / 60 >= _maxRequestsPerSecond);

        return Task.FromResult(shouldBan);
    }

    /// <inheritdoc />
    public void ClearIPAddress(IPAddress address)
    {
        _ = Requests.TryRemove(address, out _);
    }

    /// <inheritdoc />
    public void PurgeData()
    {
        long minTime = DateTime.Now.AddMinutes(-1).Ticks;

        foreach (IPAddress k in Requests.Keys)
        {
            if (!Requests.TryGetValue(k, out ConcurrentBag<long>? requests))
            {
                continue;
            }

            ConcurrentBag<long> recentRequests = new(requests.Where(x => x >= minTime));
            if (!recentRequests.Any())
            {
                _ = Requests.TryRemove(k, out _);
            }
            else
            {
                _ = Requests.AddOrUpdate(k, recentRequests, (x, y) => recentRequests);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Requests.Clear();
        }

        _disposed = true;
    }
}
