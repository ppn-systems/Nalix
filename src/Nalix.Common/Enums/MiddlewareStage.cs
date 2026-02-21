// Copyright (c) 2026 PPN Corporation. All rights reserved.

namespace Nalix.Common.Enums;

/// <summary>
/// Defines the execution stages for middleware.
/// </summary>
public enum MiddlewareStage : System.Byte
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