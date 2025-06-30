// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System;

namespace Nalix.Common.Serialization;

/// <summary>
/// Specifies that a field or property should be ignored during serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
public sealed class SerializeIgnoreAttribute : Attribute;
