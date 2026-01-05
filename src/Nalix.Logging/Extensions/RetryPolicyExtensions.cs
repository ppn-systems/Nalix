// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Core;
using Nalix.Logging.Options;

namespace Nalix.Logging.Extensions;

/// <summary>
/// Provides extension methods for retry policy integration.
/// </summary>
public static class RetryPolicyExtensions
{
    /// <summary>
    /// Creates an exponential backoff retry policy.
    /// </summary>
    /// <param name="options">The retry policy options.</param>
    /// <returns>An instance of <see cref="IRetryPolicy"/>.</returns>
    public static IRetryPolicy ToRetryPolicy(this RetryPolicyOptions options)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return options.Strategy switch
        {
            RetryStrategy.None => new NoRetryPolicy(),
            RetryStrategy.FixedDelay => new FixedDelayRetryPolicy(options),
            RetryStrategy.ExponentialBackoff or
            RetryStrategy.ExponentialBackoffWithJitter => new ExponentialBackoffRetryPolicy(options),
            _ => throw new System.ArgumentException(
                $"Unsupported retry strategy: {options.Strategy}", nameof(options))
        };
    }

    /// <summary>
    /// Creates a retry policy with exponential backoff configuration.
    /// </summary>
    /// <param name="configure">Action to configure retry policy options.</param>
    /// <returns>An instance of <see cref="IRetryPolicy"/>.</returns>
    public static IRetryPolicy CreateExponentialBackoff(System.Action<RetryPolicyOptions>? configure = null)
    {
        var options = new RetryPolicyOptions
        {
            Strategy = RetryStrategy.ExponentialBackoff
        };

        configure?.Invoke(options);

        return new ExponentialBackoffRetryPolicy(options);
    }

    /// <summary>
    /// Creates a retry policy with fixed delay configuration.
    /// </summary>
    /// <param name="configure">Action to configure retry policy options.</param>
    /// <returns>An instance of <see cref="IRetryPolicy"/>.</returns>
    public static IRetryPolicy CreateFixedDelay(System.Action<RetryPolicyOptions>? configure = null)
    {
        var options = new RetryPolicyOptions
        {
            Strategy = RetryStrategy.FixedDelay
        };

        configure?.Invoke(options);

        return new FixedDelayRetryPolicy(options);
    }
}

/// <summary>
/// A no-op retry policy that never retries.
/// </summary>
internal sealed class NoRetryPolicy : IRetryPolicy
{
    public System.Boolean ShouldRetry(System.Exception exception, System.Int32 attemptNumber) => false;

    public System.TimeSpan GetRetryDelay(System.Int32 attemptNumber) => System.TimeSpan.Zero;

    public void Execute(System.Action action, System.Action<System.Exception, System.Int32>? onRetry = null)
    {
        System.ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public System.Threading.Tasks.Task ExecuteAsync(
        System.Func<System.Threading.Tasks.Task> action,
        System.Action<System.Exception, System.Int32>? onRetry = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(action);
        return action();
    }
}
