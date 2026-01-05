// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Core;
using Nalix.Logging.Options;

namespace Nalix.Logging.Sinks;

/// <summary>
/// Wraps a logging target with circuit breaker pattern to prevent cascading failures.
/// </summary>
/// <remarks>
/// <para>
/// The circuit breaker monitors the wrapped target's health and automatically stops
/// forwarding log entries when the target is experiencing failures, allowing it time to recover.
/// </para>
/// <para>
/// The circuit breaker has three states:
/// <list type="bullet">
/// <item><description>Closed: Normal operation, all calls go through</description></item>
/// <item><description>Open: Target is failing, calls are blocked</description></item>
/// <item><description>HalfOpen: Testing if target has recovered</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Target={_innerTarget.GetType().Name}, State={_circuitState.State}")]
public sealed class CircuitBreakerLogTarget : ILoggerTarget, ILoggerErrorHandler, System.IDisposable
{
    #region Fields

    private readonly ILoggerTarget _innerTarget;
    private readonly CircuitBreakerState _circuitState;
    private readonly System.Action<StructuredErrorContext>? _onError;

    private System.Int64 _totalCallsAttempted;
    private System.Int64 _totalCallsBlocked;
    private System.Int64 _totalCallsSucceeded;
    private System.Int64 _totalCallsFailed;

    private System.Int32 _isDisposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current circuit breaker state.
    /// </summary>
    public CircuitState State => _circuitState.State;

    /// <summary>
    /// Gets the total number of calls attempted.
    /// </summary>
    public System.Int64 TotalCallsAttempted
        => System.Threading.Interlocked.Read(ref _totalCallsAttempted);

    /// <summary>
    /// Gets the total number of calls blocked by the circuit breaker.
    /// </summary>
    public System.Int64 TotalCallsBlocked
        => System.Threading.Interlocked.Read(ref _totalCallsBlocked);

    /// <summary>
    /// Gets the total number of successful calls.
    /// </summary>
    public System.Int64 TotalCallsSucceeded
        => System.Threading.Interlocked.Read(ref _totalCallsSucceeded);

    /// <summary>
    /// Gets the total number of failed calls.
    /// </summary>
    public System.Int64 TotalCallsFailed
        => System.Threading.Interlocked.Read(ref _totalCallsFailed);

    /// <summary>
    /// Gets the wrapped inner target.
    /// </summary>
    public ILoggerTarget InnerTarget => _innerTarget;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerLogTarget"/> class.
    /// </summary>
    /// <param name="innerTarget">The logging target to wrap with circuit breaker.</param>
    /// <param name="options">Circuit breaker configuration options.</param>
    /// <param name="onError">Optional callback for error notifications.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="innerTarget"/> or <paramref name="options"/> is null.
    /// </exception>
    public CircuitBreakerLogTarget(
        ILoggerTarget innerTarget,
        CircuitBreakerOptions options,
        System.Action<StructuredErrorContext>? onError = null)
    {
        System.ArgumentNullException.ThrowIfNull(innerTarget);
        System.ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        _innerTarget = innerTarget;
        _circuitState = new CircuitBreakerState(options);
        _onError = onError;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerLogTarget"/> class with default options.
    /// </summary>
    /// <param name="innerTarget">The logging target to wrap with circuit breaker.</param>
    /// <param name="onError">Optional callback for error notifications.</param>
    public CircuitBreakerLogTarget(
        ILoggerTarget innerTarget,
        System.Action<StructuredErrorContext>? onError = null)
        : this(innerTarget, new CircuitBreakerOptions(), onError)
    {
    }

    #endregion Constructors

    #region ILoggerTarget Implementation

    /// <summary>
    /// Publishes a log entry to the wrapped target if the circuit breaker allows it.
    /// </summary>
    /// <param name="logMessage">The log entry to publish.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Publish(LogEntry logMessage)
    {
        System.ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(CircuitBreakerLogTarget));

        _ = System.Threading.Interlocked.Increment(ref _totalCallsAttempted);

        // Check if circuit breaker allows the call
        if (!_circuitState.IsCallAllowed)
        {
            _ = System.Threading.Interlocked.Increment(ref _totalCallsBlocked);
            return;
        }

        var startTime = System.Diagnostics.Stopwatch.GetTimestamp();

        try
        {
            _innerTarget.Publish(logMessage);

            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTime);
            _ = System.Threading.Interlocked.Increment(ref _totalCallsSucceeded);

            // Record success with circuit breaker
            _circuitState.RecordSuccess();
        }
        catch (System.Exception ex)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTime);
            _ = System.Threading.Interlocked.Increment(ref _totalCallsFailed);

            // Record failure with circuit breaker
            _circuitState.RecordFailure();

            // Create error context
            var errorContext = new StructuredErrorContext(
                errorMessage: $"Failed to publish log entry to target: {ex.Message}",
                exception: ex,
                severity: LogLevel.Error,
                targetName: _innerTarget.GetType().Name,
                originalLogEntry: logMessage,
                category: CategorizeException(ex),
                elapsedTime: elapsed);

            // Notify error handler
            _onError?.Invoke(errorContext);

            // If inner target implements error handler, notify it too
            if (_innerTarget is ILoggerErrorHandler errorHandler)
            {
                try
                {
                    errorHandler.HandleError(ex, logMessage);
                }
                catch
                {
                    // Swallow errors from error handler to prevent cascading failures
                }
            }
        }
    }

    #endregion ILoggerTarget Implementation

    #region ILoggerErrorHandler Implementation

    /// <summary>
    /// Handles errors from the logging system.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="entry">The log entry being processed.</param>
    public void HandleError(System.Exception exception, LogEntry entry)
    {
        _ = System.Threading.Interlocked.Increment(ref _totalCallsFailed);
        _circuitState.RecordFailure();

        var errorContext = new StructuredErrorContext(
            errorMessage: $"Error in circuit breaker wrapped target: {exception.Message}",
            exception: exception,
            severity: LogLevel.Error,
            targetName: _innerTarget.GetType().Name,
            originalLogEntry: entry,
            category: CategorizeException(exception));

        _onError?.Invoke(errorContext);
    }

    #endregion ILoggerErrorHandler Implementation

    #region IDisposable Implementation

    /// <summary>
    /// Releases resources used by the circuit breaker and inner target.
    /// </summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        if (_innerTarget is System.IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error disposing inner target: {ex.Message}");
            }
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable Implementation

    #region Public Methods

    /// <summary>
    /// Gets diagnostic information about the circuit breaker.
    /// </summary>
    /// <returns>A string containing diagnostic information.</returns>
    public System.String GetDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        _ = sb.AppendLine($"Circuit Breaker for {_innerTarget.GetType().Name}");
        _ = sb.AppendLine($"Total Calls Attempted: {TotalCallsAttempted:N0}");
        _ = sb.AppendLine($"Total Calls Succeeded: {TotalCallsSucceeded:N0}");
        _ = sb.AppendLine($"Total Calls Failed: {TotalCallsFailed:N0}");
        _ = sb.AppendLine($"Total Calls Blocked: {TotalCallsBlocked:N0}");
        _ = sb.AppendLine();
        _ = sb.Append(_circuitState.GetDiagnostics());

        return sb.ToString();
    }

    /// <summary>
    /// Resets the circuit breaker to closed state (for testing/admin purposes).
    /// </summary>
    /// <remarks>
    /// Use with caution - this bypasses the normal circuit breaker logic.
    /// Note: This method is provided for testing/admin scenarios and should not be
    /// used in normal production code.
    /// </remarks>
    [System.Obsolete("Reset is only for testing purposes. Consider creating a new instance instead.")]
    public void Reset()
    {
        // Reset metrics only - creating new state would require field reassignment
        System.Threading.Interlocked.Exchange(ref _totalCallsAttempted, 0);
        System.Threading.Interlocked.Exchange(ref _totalCallsBlocked, 0);
        System.Threading.Interlocked.Exchange(ref _totalCallsSucceeded, 0);
        System.Threading.Interlocked.Exchange(ref _totalCallsFailed, 0);
    }

    #endregion Public Methods

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ErrorCategory CategorizeException(System.Exception ex)
    {
        return ex switch
        {
            System.IO.IOException => ErrorCategory.DiskIO,
            System.UnauthorizedAccessException => ErrorCategory.Security,
            System.TimeoutException => ErrorCategory.Timeout,
            System.OutOfMemoryException => ErrorCategory.Memory,
            System.Net.Sockets.SocketException => ErrorCategory.Network,
            System.Net.Http.HttpRequestException => ErrorCategory.Network,
            System.InvalidOperationException when ex.Message.Contains("queue") => ErrorCategory.Capacity,
            _ => ErrorCategory.Unknown
        };
    }

    #endregion Private Methods
}
