// Copyright (c) 2025 PPN Corporation. All rights reserved. 

namespace Nalix.Common.Core.Enums;

/// <summary>
/// Defines strategies for distributing log messages across multiple webhooks.
/// </summary>
public enum WebhookDispatchMode
{
    /// <summary>
    /// Distributes requests in a circular order across all webhooks.
    /// </summary>
    RoundRobin = 0,

    /// <summary>
    /// Randomly selects a webhook for each batch.
    /// </summary>
    Random = 1,

    /// <summary>
    /// Always uses the first available webhook (fallback to next on failure).
    /// </summary>
    Failover = 2
}