// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Validation;

/// <summary>
/// Specifies that a property value must not be <see langword="null"/>.
/// AOT-safe replacement for <c>System.ComponentModel.DataAnnotations.RequiredAttribute</c>.
/// </summary>
/// <remarks>
/// The source generator emits a null-check for reference types and nullable value types.
/// For string properties that must also be non-empty, prefer <see cref="LengthAttribute"/>
/// with a minimum of 1.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class RequiredAttribute : Attribute
{
}
