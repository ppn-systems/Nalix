namespace Notio.Network.Firewall.Metadata;

/// <summary>
/// Represents the data of a request, including the history of request timestamps and optional block expiration time.
/// </summary>
internal readonly record struct RequestDataInfo(
    System.Collections.Generic.Queue<System.DateTime> Requests,
    System.DateTime? BlockedUntil
);
