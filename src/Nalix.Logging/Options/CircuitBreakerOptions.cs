// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Options;

/// <summary>
/// Configuration options for circuit breaker behavior in logging targets.
/// </summary>
/// <remarks>
/// Circuit breakers prevent cascading failures by temporarily stopping calls to a failing target,
/// allowing it time to recover. The circuit breaker transitions through three states:
/// Closed (normal operation), Open (blocking calls), and HalfOpen (testing recovery).
/// </remarks>
public sealed class CircuitBreakerOptions
{
    #region Properties

    /// <summary>
    /// Gets or sets the number of consecutive failures before opening the circuit.
    /// </summary>
    /// <remarks>
    /// Default is 5 failures. Must be greater than 0.
    /// </remarks>
    public System.Int32 FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration to wait before attempting to close the circuit after it opens.
    /// </summary>
    /// <remarks>
    /// Default is 30 seconds. Must be greater than TimeSpan.Zero.
    /// </remarks>
    public System.TimeSpan OpenDuration { get; set; } = System.TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the number of successful calls required in HalfOpen state to close the circuit.
    /// </summary>
    /// <remarks>
    /// Default is 2 successes. Must be greater than 0.
    /// </remarks>
    public System.Int32 SuccessThreshold { get; set; } = 2;

    /// <summary>
    /// Gets or sets the time window for counting failures.
    /// </summary>
    /// <remarks>
    /// Default is 60 seconds. Failures older than this window are not counted.
    /// Must be greater than TimeSpan.Zero.
    /// </remarks>
    public System.TimeSpan FailureWindow { get; set; } = System.TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets whether to use exponential backoff for the open duration.
    /// </summary>
    /// <remarks>
    /// When enabled, each consecutive circuit open will double the open duration,
    /// up to the maximum specified in <see cref="MaxOpenDuration"/>.
    /// Default is true.
    /// </remarks>
    public System.Boolean UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum open duration when using exponential backoff.
    /// </summary>
    /// <remarks>
    /// Default is 5 minutes. Only applies when <see cref="UseExponentialBackoff"/> is true.
    /// Must be greater than <see cref="OpenDuration"/>.
    /// </remarks>
    public System.TimeSpan MaxOpenDuration { get; set; } = System.TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to log circuit breaker state changes.
    /// </summary>
    /// <remarks>
    /// Default is true. When enabled, state transitions are logged to Debug output.
    /// </remarks>
    public System.Boolean LogStateChanges { get; set; } = true;

    #endregion Properties

    #region Methods

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="System.ArgumentException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (FailureThreshold <= 0)
        {
            throw new System.ArgumentException(
                $"{nameof(FailureThreshold)} must be greater than 0.", nameof(FailureThreshold));
        }

        if (OpenDuration <= System.TimeSpan.Zero)
        {
            throw new System.ArgumentException(
                $"{nameof(OpenDuration)} must be greater than TimeSpan.Zero.", nameof(OpenDuration));
        }

        if (SuccessThreshold <= 0)
        {
            throw new System.ArgumentException(
                $"{nameof(SuccessThreshold)} must be greater than 0.", nameof(SuccessThreshold));
        }

        if (FailureWindow <= System.TimeSpan.Zero)
        {
            throw new System.ArgumentException(
                $"{nameof(FailureWindow)} must be greater than TimeSpan.Zero.", nameof(FailureWindow));
        }

        if (UseExponentialBackoff && MaxOpenDuration <= OpenDuration)
        {
            throw new System.ArgumentException(
                $"{nameof(MaxOpenDuration)} must be greater than {nameof(OpenDuration)} when using exponential backoff.",
                nameof(MaxOpenDuration));
        }
    }

    #endregion Methods
}
