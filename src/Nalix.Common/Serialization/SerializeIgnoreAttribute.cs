// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
namespace Nalix.Common.Serialization;

/// <summary>
/// Specifies that a field or property should be ignored during serialization.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property, Inherited = true)]
public sealed class SerializeIgnoreAttribute : System.Attribute;