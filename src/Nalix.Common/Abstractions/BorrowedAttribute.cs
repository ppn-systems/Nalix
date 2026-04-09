// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Abstractions;

/// <summary>
/// Indicates that a parameter of type <see cref="IBufferLease"/> is borrowed by the method 
/// and should not be disposed of or returned as a result of ownership transfer.
/// </summary>
/// <remarks>
/// This helps Nalix static analysis tools identify that the buffer lifecycle is managed elsewhere.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class BorrowedAttribute : Attribute
{
}
