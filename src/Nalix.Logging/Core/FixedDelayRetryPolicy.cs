// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Options;

namespace Nalix.Logging.Core;

/// <summary>
/// Implements a fixed delay retry policy.
/// </summary>
/// <remarks>
/// Fixed delay retry policy waits for a constant duration between retry attempts.
/// This is simpler than exponential backoff but may not be as effective for
/// preventing thundering herd problems.
/// </remarks>
public sealed class FixedDelayRetryPolicy : IRetryPolicy
{
    #region Fields

    private readonly RetryPolicyOptions _options;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedDelayRetryPolicy"/> class.
    /// </summary>
    /// <param name="options">The retry policy configuration options.</param>
    public FixedDelayRetryPolicy(RetryPolicyOptions options)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _options = options;
    }

    /// <summary>
    /// Initializes a new instance with default options.
    /// </summary>
    public FixedDelayRetryPolicy()
        : this(new RetryPolicyOptions { Strategy = RetryStrategy.FixedDelay })
    {
    }

    #endregion Constructors

    #region IRetryPolicy Implementation

    /// <inheritdoc/>
    public System.Boolean ShouldRetry(System.Exception exception, System.Int32 attemptNumber)
    {
        if (attemptNumber > _options.MaxRetryAttempts)
        {
            return false;
        }

        // Check if we should retry based on exception type
        if (_options.RetryOnAllExceptions)
        {
            return true;
        }

        // Only retry on transient failures
        return IsTransientException(exception);
    }

    /// <inheritdoc/>
    public System.TimeSpan GetRetryDelay(System.Int32 attemptNumber)
    {
        // Fixed delay - always return the initial delay
        return attemptNumber > 0 ? _options.InitialDelay : System.TimeSpan.Zero;
    }

    /// <inheritdoc/>
    public void Execute(System.Action action, System.Action<System.Exception, System.Int32>? onRetry = null)
    {
        System.ArgumentNullException.ThrowIfNull(action);

        System.Int32 attemptNumber = 0;

        while (true)
        {
            attemptNumber++;

            try
            {
                action();
                return; // Success
            }
            catch (System.Exception ex)
            {
                if (!ShouldRetry(ex, attemptNumber))
                {
                    throw;
                }

                onRetry?.Invoke(ex, attemptNumber);

                var delay = GetRetryDelay(attemptNumber);
                if (delay > System.TimeSpan.Zero)
                {
                    System.Threading.Thread.Sleep(delay);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task ExecuteAsync(
        System.Func<System.Threading.Tasks.Task> action,
        System.Action<System.Exception, System.Int32>? onRetry = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(action);

        System.Int32 attemptNumber = 0;

        while (true)
        {
            attemptNumber++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await action().ConfigureAwait(false);
                return; // Success
            }
            catch (System.Exception ex) when (ex is not System.OperationCanceledException)
            {
                if (!ShouldRetry(ex, attemptNumber))
                {
                    throw;
                }

                onRetry?.Invoke(ex, attemptNumber);

                var delay = GetRetryDelay(attemptNumber);
                if (delay > System.TimeSpan.Zero)
                {
                    await System.Threading.Tasks.Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    #endregion IRetryPolicy Implementation

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsTransientException(System.Exception exception)
    {
        return exception switch
        {
            System.TimeoutException => true,
            System.IO.IOException io when IsTransientIOException(io) => true,
            System.Net.Sockets.SocketException socket when IsTransientSocketException(socket) => true,
            System.Net.Http.HttpRequestException => true,
            _ => false
        };
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsTransientIOException(System.IO.IOException exception)
    {
        const System.Int32 ERROR_SHARING_VIOLATION = 32;
        const System.Int32 ERROR_LOCK_VIOLATION = 33;

        var hResult = exception.HResult & 0xFFFF;
        return hResult == ERROR_SHARING_VIOLATION || hResult == ERROR_LOCK_VIOLATION;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsTransientSocketException(System.Net.Sockets.SocketException exception)
    {
        return exception.SocketErrorCode switch
        {
            System.Net.Sockets.SocketError.TimedOut => true,
            System.Net.Sockets.SocketError.ConnectionRefused => true,
            System.Net.Sockets.SocketError.ConnectionReset => true,
            System.Net.Sockets.SocketError.NetworkUnreachable => true,
            System.Net.Sockets.SocketError.HostUnreachable => true,
            _ => false
        };
    }

    #endregion Private Methods
}
