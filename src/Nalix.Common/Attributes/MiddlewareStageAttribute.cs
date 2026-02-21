// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Common.Attributes;

/// <summary>
/// Specifies the execution stage of middleware in the pipeline.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class MiddlewareStageAttribute : System.Attribute
{
    /// <summary>
    /// Gets the execution stage of the middleware.
    /// </summary>
    public MiddlewareStage Stage { get; }

    /// <summary>
    /// Gets a value indicating whether this middleware should always execute in outbound,
    /// even when <c>PacketContext&lt;TPacket&gt;.SkipOutbound</c> is true.
    /// </summary>
    public System.Boolean AlwaysExecute { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MiddlewareStageAttribute"/> class.
    /// </summary>
    /// <param name="stage">The execution stage.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public MiddlewareStageAttribute(MiddlewareStage stage) => Stage = stage;
}