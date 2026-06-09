// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Injection;

/// <summary>
/// Marks a class as injectable, enabling compile-time activation factory generation
/// and optional interface registration mapping.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class InjectableAttribute : Attribute
{
    /// <summary>
    /// Gets the optional interface or base class type that this service implements/inherits
    /// and should be registered as in the InstanceManager.
    /// </summary>
    public Type? ServiceType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InjectableAttribute"/> class.
    /// Specifies that only the concrete type itself should be registered.
    /// </summary>
    public InjectableAttribute() => this.ServiceType = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="InjectableAttribute"/> class.
    /// Specifies that the service should be registered under the concrete type
    /// as well as the specified <paramref name="serviceType"/> interface or base type.
    /// </summary>
    /// <param name="serviceType">The service interface or base class type to register.</param>
    public InjectableAttribute(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType, nameof(serviceType));
        this.ServiceType = serviceType;
    }
}
