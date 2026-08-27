// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Injection;

/// <summary>
/// Specifies that a packet handler method parameter should be resolved from the current <see cref="IPacketScope"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class FromScopeAttribute : Attribute
{
}
