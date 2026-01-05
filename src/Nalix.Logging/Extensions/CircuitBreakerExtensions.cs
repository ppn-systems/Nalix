// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Core;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;

namespace Nalix.Logging.Extensions;

/// <summary>
/// Provides extension methods for integrating circuit breakers with logging targets.
/// </summary>
public static class CircuitBreakerExtensions
{
    /// <summary>
    /// Wraps a logging target with circuit breaker protection.
    /// </summary>
    /// <param name="target">The target to wrap.</param>
    /// <param name="options">Circuit breaker configuration options.</param>
    /// <param name="onError">Optional callback for error notifications.</param>
    /// <returns>A circuit breaker-protected logging target.</returns>
    public static CircuitBreakerLogTarget WithCircuitBreaker(
        this ILoggerTarget target,
        CircuitBreakerOptions? options = null,
        System.Action<StructuredErrorContext>? onError = null)
    {
        System.ArgumentNullException.ThrowIfNull(target);

        return new CircuitBreakerLogTarget(
            target,
            options ?? new CircuitBreakerOptions(),
            onError);
    }

    /// <summary>
    /// Wraps a logging target with circuit breaker protection using a configuration action.
    /// </summary>
    /// <param name="target">The target to wrap.</param>
    /// <param name="configure">Action to configure circuit breaker options.</param>
    /// <param name="onError">Optional callback for error notifications.</param>
    /// <returns>A circuit breaker-protected logging target.</returns>
    public static CircuitBreakerLogTarget WithCircuitBreaker(
        this ILoggerTarget target,
        System.Action<CircuitBreakerOptions> configure,
        System.Action<StructuredErrorContext>? onError = null)
    {
        System.ArgumentNullException.ThrowIfNull(target);
        System.ArgumentNullException.ThrowIfNull(configure);

        var options = new CircuitBreakerOptions();
        configure(options);

        return new CircuitBreakerLogTarget(target, options, onError);
    }

    /// <summary>
    /// Configures the distributor to register a target with circuit breaker protection.
    /// </summary>
    /// <param name="options">The logging options.</param>
    /// <param name="target">The target to register with circuit breaker.</param>
    /// <param name="circuitBreakerOptions">Circuit breaker configuration.</param>
    /// <param name="onError">Optional callback for error notifications.</param>
    /// <returns>The logging options for method chaining.</returns>
    public static NLogixOptions RegisterTargetWithCircuitBreaker(
        this NLogixOptions options,
        ILoggerTarget target,
        CircuitBreakerOptions? circuitBreakerOptions = null,
        System.Action<StructuredErrorContext>? onError = null)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        System.ArgumentNullException.ThrowIfNull(target);

        var wrappedTarget = target.WithCircuitBreaker(circuitBreakerOptions, onError);
        return options.RegisterTarget(wrappedTarget);
    }
}
