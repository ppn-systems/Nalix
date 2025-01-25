namespace Notio.Network.Firewall.Models;

/// <summary>
/// Represents the data of a request, including the history of request timestamps and optional block expiration time.
/// </summary>
internal readonly record struct RequestDataInfo(
    /// <summary>
    /// A queue of timestamps representing the times of incoming requests.
    /// </summary>
    System.Collections.Generic.Queue<System.DateTime> Requests,

    /// <summary>
    /// The date and time until which requests are blocked, or <c>null</c> if not blocked.
    /// </summary>
    System.DateTime? BlockedUntil
);