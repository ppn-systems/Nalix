// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Middleware;

/// <summary>
/// Defines the execution stages for middleware.
/// </summary>
public enum MiddlewareStage : byte
{
    /// <summary>
    /// Executes before the main handler (inbound processing).
    /// </summary>
    Inbound = 0,

    /// <summary>
    /// Executes after the main handler (outbound processing).
    /// </summary>
    Outbound = 1,

    /// <summary>
    /// Executes in both inbound and outbound stages.
    /// </summary>
    Both = 2
}