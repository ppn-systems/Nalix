// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Attributes;

/// <summary>
/// Indicates that a class should be automatically registered 
/// with the <c>InstanceManager</c> or a DI container.
/// </summary>
/// <remarks>
/// Apply this attribute to a concrete class (non-abstract) to enable automatic
/// registration for dependency injection. If the class implements one or more
/// interfaces, it will be registered for all implemented interfaces.
/// </remarks>
/// <example>
/// <code>
/// [Service]
/// public class MyService : IMyService
/// {
///     // Implementation
/// }
/// </code>
/// </example>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
public sealed class ServiceAttribute : System.Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAttribute"/> class.
    /// </summary>
    /// <param name="lifetime">
    /// Specifies the lifetime of the service in the DI container.
    /// Defaults to <see cref="ServiceLifetime.Singleton"/>.
    /// </param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton) => Lifetime = lifetime;

    /// <summary>
    /// Gets the service lifetime for the registered class.
    /// </summary>
    public ServiceLifetime Lifetime { get; }
}

/// <summary>
/// Specifies the lifetime of a service in the dependency injection container.
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    /// A single instance is created and shared throughout the application's lifetime.
    /// </summary>
    Singleton,

    /// <summary>
    /// A new instance is created each time it is requested.
    /// </summary>
    Transient
}
