// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Enums;

/// <summary>
/// Represents the stage at which middleware is executed in the dispatch pipeline.
/// </summary>
public enum MiddlewareStage : System.Byte
{
    /// <summary>
    /// Indicates the middleware is executed before the main operation.
    /// </summary>
    Outbound = 1,

    /// <summary>
    /// Indicates the middleware is executed after the main operation.
    /// </summary>
    Inbound = 2
}
