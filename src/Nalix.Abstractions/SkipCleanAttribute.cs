// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions;

/// <summary>
/// Specifies that a class should be excluded from automatic clean-up
/// or maintenance operations.
/// </summary>
/// <remarks>
/// This attribute is typically used by components that perform clean-up,
/// pruning, or state-reset logic. When applied, it indicates that the
/// annotated class must be preserved and skipped during these operations.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class SkipCleanAttribute : Attribute
{
}
