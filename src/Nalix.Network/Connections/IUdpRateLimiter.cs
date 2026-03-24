// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Net;

namespace Nalix.Network.Connections;

/// <summary>
/// Defines a contract for a fast path rate-limiter that protects UDP listeners 
/// from DDOS or flood attacks by restricting datagrams per second on a per-IP basis.
/// </summary>
public interface IUdpRateLimiter
{
    /// <summary>
    /// Attempts to record a datagram from the given IP endpoint and checks if it exceeds the limit.
    /// </summary>
    /// <param name="endPoint">The source endpoint of the incoming datagram.</param>
    /// <returns><c>true</c> if the datagram is allowed; <c>false</c> if it must be dropped.</returns>
    bool TryAccept(IPEndPoint endPoint);
}
