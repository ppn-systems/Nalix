// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Serialization;

/// <summary>
/// Marks a type so that the <c>SerializeFormatterGenerator</c> will automatically generate
/// a high-performance formatter for it at compile time.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is intended for types used in low-latency TCP networking and messaging scenarios.
/// When applied, the source generator will emit an optimized <c>*Formatter</c> implementation
/// that implements <c>IFormatter&lt;T&gt;</c> or <c>IFillableFormatter&lt;T&gt;</c>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class GenerateFormatterAttribute : Attribute
{
}
