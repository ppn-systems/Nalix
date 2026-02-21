// Copyright (c) 2026 PPN Corporation. All rights reserved.

namespace Nalix.Common.Attributes;

/// <summary>
/// Specifies the execution order of middleware in the pipeline.
/// Lower values execute first in inbound processing and last in outbound processing.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class MiddlewareOrderAttribute : System.Attribute
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
    public System.Int32 Order { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MiddlewareOrderAttribute"/> class.
    /// </summary>
    /// <param name="order">The execution order value.</param>
    public MiddlewareOrderAttribute(System.Int32 order) => Order = order;
}