using System.Collections.Generic;

namespace Notio.Network.Security.Metadata;

/// <summary>
/// Represents the data of a request, including the history of request timestamps and optional block expiration time.
/// </summary>
internal readonly struct RequestLimiterInfo
{
    private readonly Queue<long> _requests;
    public long BlockedUntilTicks { get; }
    public long LastRequestTicks { get; }
    public int RequestCount => _requests.Count;
    public IReadOnlyCollection<long> Requests => _requests;

    public RequestLimiterInfo(long firstRequest)
    {
        _requests = new Queue<long>(capacity: 8);
        _requests.Enqueue(firstRequest);
        BlockedUntilTicks = 0;
        LastRequestTicks = firstRequest;
    }

    public RequestLimiterInfo Process(long now, int maxRequests, long windowTicks, long blockDurationTicks)
    {
        while (_requests.Count > 0 && now - _requests.Peek() > windowTicks)
            _requests.Dequeue();

        if (_requests.Count >= maxRequests)
        {
            return new RequestLimiterInfo(_requests, now + blockDurationTicks, now);
        }

        _requests.Enqueue(now);
        return new RequestLimiterInfo(_requests, 0, now);
    }

    public void Cleanup(long now, long windowTicks)
    {
        while (_requests.Count > 0 && now - _requests.Peek() > windowTicks)
            _requests.Dequeue();
    }

    private RequestLimiterInfo(Queue<long> requests, long blockedUntilTicks, long lastRequestTicks)
    {
        _requests = requests;
        BlockedUntilTicks = blockedUntilTicks;
        LastRequestTicks = lastRequestTicks;
    }
}
