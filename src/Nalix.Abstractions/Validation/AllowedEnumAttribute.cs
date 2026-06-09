// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Validation;

/// <summary>
/// Specifies that a property value must be a defined member of its enum type.
/// AOT-safe replacement for <c>System.ComponentModel.DataAnnotations.EnumDataTypeAttribute</c>.
/// </summary>
/// <remarks>
/// The source generator emits an <c>Enum.IsDefined()</c> check at compile time.
/// No runtime reflection or DataAnnotations dependency is required.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class AllowedEnumAttribute : Attribute
{
}
