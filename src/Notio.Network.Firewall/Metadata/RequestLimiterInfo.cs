using System;
using System.Collections.Generic;

namespace Notio.Network.Firewall.Metadata;

/// <summary>
/// Represents the data of a request, including the history of request timestamps and optional block expiration time.
/// </summary>
internal readonly record struct RequestLimiterInfo(
    Queue<DateTime> Requests,
    DateTime? BlockedUntil
);
