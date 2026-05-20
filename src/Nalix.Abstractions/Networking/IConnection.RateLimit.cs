// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;

namespace Nalix.Abstractions.Networking;

public partial interface IConnection
{
    /// <summary>
    /// Gets a thread-safe cache used for caching connection-level rate limiting endpoints.
    /// </summary>
    ConcurrentDictionary<ushort, object> RateLimitCache { get; }
}
