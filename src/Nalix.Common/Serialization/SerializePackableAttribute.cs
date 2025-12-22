// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Serialization;

/// <summary>
/// An attribute that marks a class, struct, or interface as serializable with a specified layout.
/// This attribute is used to configure the serialization behavior for types in the Nalix serialization framework.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface, Inherited = true)]
public sealed class SerializePackableAttribute(SerializeLayout layout) : Attribute
{
    /// <summary>
    /// Gets the layout strategy to be used during serialization of the marked type.
    /// The default value is <see cref="SerializeLayout.Sequential"/> if not explicitly set.
    /// </summary>
    public SerializeLayout SerializeLayout { get; set; } = layout;
}
