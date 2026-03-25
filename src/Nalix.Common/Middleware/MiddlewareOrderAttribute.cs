// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Middleware;

/// <summary>
/// Specifies the execution order of middleware in the pipeline.
/// Lower values execute first in inbound processing and last in outbound processing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MiddlewareOrderAttribute"/> class.
/// </remarks>
/// <param name="order">The execution order value.</param>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class MiddlewareOrderAttribute(System.Int32 order) : System.Attribute
{
    /// <summary>
    /// Gets the execution order of the middleware.
    /// </summary>
    /// <remarks>
    /// Default is 0. Negative values execute before default, positive values execute after.
    /// Common values:
    /// - -100: Critical pre-processing (e.g., unwrapping, decryption)
    /// - -50: Security checks (e.g., permission validation)
    /// - 0: Default order
    /// - 50: Business logic constraints (e.g., rate limiting, concurrency)
    /// - 100: Post-processing (e.g., wrapping, encryption)
    /// </remarks>
    public System.Int32 Order { get; } = order;
}