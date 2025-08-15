// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Network.Throttling.Metadata;

/// <summary>
/// Represents the data of a request, including the history of request timestamps and optional block expiration time.
/// </summary>
internal readonly struct RequestLimiterInfo
{
    #region Fields

    private readonly System.Collections.Generic.Queue<System.Int64> _requests;

    #endregion Fields

    #region Properties

    public System.Int64 BlockedUntilTicks { get; }
    public System.Int64 LastRequestTicks { get; }
    public System.Int32 RequestCount => this._requests.Count;
    public System.Collections.Generic.IReadOnlyCollection<System.Int64> Requests => this._requests;

    #endregion Properties

    #region Constructors

    public RequestLimiterInfo(System.Int64 firstRequest)
    {
        this._requests = new System.Collections.Generic.Queue<System.Int64>(capacity: 8);
        this._requests.Enqueue(firstRequest);
        this.BlockedUntilTicks = 0;
        this.LastRequestTicks = firstRequest;
    }

    #endregion Constructors

    #region Public Methods

    public RequestLimiterInfo Process(System.Int64 now, System.Int32 maxRequests, System.Int64 windowTicks, System.Int64 blockDurationTicks)
    {
        while (this._requests.Count > 0 && now - this._requests.Peek() > windowTicks)
        {
            _ = this._requests.Dequeue();
        }

        if (this._requests.Count >= maxRequests)
        {
            return new RequestLimiterInfo(this._requests, now + blockDurationTicks, now);
        }

        this._requests.Enqueue(now);
        return new RequestLimiterInfo(this._requests, 0, now);
    }

    public void Cleanup(System.Int64 now, System.Int64 windowTicks)
    {
        while (this._requests.Count > 0 && now - this._requests.Peek() > windowTicks)
        {
            _ = this._requests.Dequeue();
        }
    }

    #endregion Public Methods

    #region Private Methods

    private RequestLimiterInfo(
        System.Collections.Generic.Queue<System.Int64> requests,
        System.Int64 blockedUntilTicks, System.Int64 lastRequestTicks)
    {
        this._requests = requests;
        this.BlockedUntilTicks = blockedUntilTicks;
        this.LastRequestTicks = lastRequestTicks;
    }

    #endregion Private Methods
}
