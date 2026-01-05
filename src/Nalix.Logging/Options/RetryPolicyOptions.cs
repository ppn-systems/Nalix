// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Options;

/// <summary>
/// Defines the retry strategy to use.
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// No retry - fail immediately.
    /// </summary>
    None = 0,

    /// <summary>
    /// Fixed delay between retries.
    /// </summary>
    FixedDelay = 1,

    /// <summary>
    /// Exponentially increasing delay between retries.
    /// </summary>
    ExponentialBackoff = 2,

    /// <summary>
    /// Exponential backoff with jitter to prevent thundering herd.
    /// </summary>
    ExponentialBackoffWithJitter = 3
}

/// <summary>
/// Configuration options for retry policies in logging operations.
/// </summary>
/// <remarks>
/// Retry policies help handle transient failures by automatically retrying failed operations
/// with configurable delays and strategies.
/// </remarks>
public sealed class RetryPolicyOptions
{
    #region Properties

    /// <summary>
    /// Gets or sets the retry strategy to use.
    /// </summary>
    /// <remarks>
    /// Default is <see cref="RetryStrategy.ExponentialBackoffWithJitter"/>.
    /// </remarks>
    public RetryStrategy Strategy { get; set; } = RetryStrategy.ExponentialBackoffWithJitter;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    /// <remarks>
    /// Default is 3 attempts. Set to 0 to disable retries.
    /// Must be non-negative.
    /// </remarks>
    public System.Int32 MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay between retry attempts.
    /// </summary>
    /// <remarks>
    /// Default is 100 milliseconds. For exponential backoff, this is the base delay.
    /// Must be greater than TimeSpan.Zero.
    /// </remarks>
    public System.TimeSpan InitialDelay { get; set; } = System.TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the maximum delay between retry attempts.
    /// </summary>
    /// <remarks>
    /// Default is 10 seconds. Prevents exponential backoff from growing too large.
    /// Must be greater than or equal to <see cref="InitialDelay"/>.
    /// </remarks>
    public System.TimeSpan MaxDelay { get; set; } = System.TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the exponential backoff multiplier.
    /// </summary>
    /// <remarks>
    /// Default is 2.0. Each retry delay is multiplied by this factor.
    /// Only applies to exponential backoff strategies.
    /// Must be greater than 1.0.
    /// </remarks>
    public System.Double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets whether to retry on all exceptions or only specific ones.
    /// </summary>
    /// <remarks>
    /// Default is false (retry only transient failures).
    /// When true, all exceptions trigger retry logic.
    /// </remarks>
    public System.Boolean RetryOnAllExceptions { get; set; } = false;

    /// <summary>
    /// Gets or sets the timeout for each retry attempt.
    /// </summary>
    /// <remarks>
    /// Default is 30 seconds. Operations exceeding this timeout will be cancelled.
    /// Set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> for no timeout.
    /// </remarks>
    public System.TimeSpan RetryTimeout { get; set; } = System.TimeSpan.FromSeconds(30);

    #endregion Properties

    #region Methods

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="System.ArgumentException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (MaxRetryAttempts < 0)
        {
            throw new System.ArgumentException(
                $"{nameof(MaxRetryAttempts)} must be non-negative.", nameof(MaxRetryAttempts));
        }

        if (InitialDelay <= System.TimeSpan.Zero)
        {
            throw new System.ArgumentException(
                $"{nameof(InitialDelay)} must be greater than TimeSpan.Zero.", nameof(InitialDelay));
        }

        if (MaxDelay < InitialDelay)
        {
            throw new System.ArgumentException(
                $"{nameof(MaxDelay)} must be greater than or equal to {nameof(InitialDelay)}.",
                nameof(MaxDelay));
        }

        if (BackoffMultiplier <= 1.0)
        {
            throw new System.ArgumentException(
                $"{nameof(BackoffMultiplier)} must be greater than 1.0.", nameof(BackoffMultiplier));
        }

        if (RetryTimeout != System.Threading.Timeout.InfiniteTimeSpan && RetryTimeout <= System.TimeSpan.Zero)
        {
            throw new System.ArgumentException(
                $"{nameof(RetryTimeout)} must be greater than TimeSpan.Zero or InfiniteTimeSpan.",
                nameof(RetryTimeout));
        }
    }

    #endregion Methods
}
