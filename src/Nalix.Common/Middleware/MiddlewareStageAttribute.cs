// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Nalix.Common.Middleware;

/// <summary>
/// Specifies the execution stage of middleware in the pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class MiddlewareStageAttribute : Attribute
{
    /// <summary>
    /// Gets the execution stage of the middleware.
    /// </summary>
    public MiddlewareStage Stage { get; }

    /// <summary>
    /// Gets a value indicating whether this middleware should always execute in outbound,
    /// even when <c>PacketContext&lt;TPacket&gt;.SkipOutbound</c> is true.
    /// </summary>
    public bool AlwaysExecute { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MiddlewareStageAttribute"/> class.
    /// </summary>
    /// <param name="stage">The execution stage.</param>
    [SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public MiddlewareStageAttribute(MiddlewareStage stage) => Stage = stage;
}
