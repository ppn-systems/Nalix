// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Core;

/// <summary>
/// Defines a retry policy for handling transient failures in logging operations.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Determines whether a retry should be attempted for the given exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="attemptNumber">The current attempt number (1-based).</param>
    /// <returns>True if a retry should be attempted; otherwise, false.</returns>
    System.Boolean ShouldRetry(System.Exception exception, System.Int32 attemptNumber);

    /// <summary>
    /// Calculates the delay before the next retry attempt.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (1-based).</param>
    /// <returns>The time span to wait before retrying.</returns>
    System.TimeSpan GetRetryDelay(System.Int32 attemptNumber);

    /// <summary>
    /// Executes an action with retry logic.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="onRetry">Optional callback invoked before each retry.</param>
    void Execute(System.Action action, System.Action<System.Exception, System.Int32>? onRetry = null);

    /// <summary>
    /// Executes an action with retry logic asynchronously.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="onRetry">Optional callback invoked before each retry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    System.Threading.Tasks.Task ExecuteAsync(
        System.Func<System.Threading.Tasks.Task> action,
        System.Action<System.Exception, System.Int32>? onRetry = null,
        System.Threading.CancellationToken cancellationToken = default);
}
