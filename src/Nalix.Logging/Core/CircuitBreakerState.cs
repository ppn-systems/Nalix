// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Core;

/// <summary>
/// Represents the possible states of a circuit breaker.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed; operations are allowed to proceed normally.
    /// </summary>
    Closed = 0,

    /// <summary>
    /// Circuit is open; operations are blocked to prevent cascading failures.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Circuit is half-open; testing if the target has recovered.
    /// </summary>
    HalfOpen = 2
}

/// <summary>
/// Manages circuit breaker state transitions with thread-safe operations.
/// </summary>
/// <remarks>
/// The circuit breaker pattern prevents cascading failures by temporarily stopping calls
/// to a failing target, allowing it time to recover. This implementation uses lock-free
/// operations for high performance.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("State={State}, Failures={_failureCount}, Successes={_successCount}")]
internal sealed class CircuitBreakerState
{
    #region Fields

    private readonly Options.CircuitBreakerOptions _options;
    private readonly System.Collections.Concurrent.ConcurrentQueue<System.DateTime> _failureTimestamps;

    private System.Int32 _state; // CircuitState as Int32 for Interlocked operations
    private System.Int32 _failureCount;
    private System.Int32 _successCount;
    private System.Int64 _lastStateChangeTicks;
    private System.Int64 _consecutiveOpenCount;
    private System.Int32 _testCallInProgress;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    public CircuitState State
    {
        get => (CircuitState)System.Threading.Volatile.Read(ref _state);
        private set => System.Threading.Volatile.Write(ref _state, (System.Int32)value);
    }

    /// <summary>
    /// Gets the timestamp of the last state change.
    /// </summary>
    public System.DateTime LastStateChange
        => new(System.Threading.Interlocked.Read(ref _lastStateChangeTicks), System.DateTimeKind.Utc);

    /// <summary>
    /// Gets the current failure count.
    /// </summary>
    public System.Int32 FailureCount
        => System.Threading.Volatile.Read(ref _failureCount);

    /// <summary>
    /// Gets the current success count (only relevant in HalfOpen state).
    /// </summary>
    public System.Int32 SuccessCount
        => System.Threading.Volatile.Read(ref _successCount);

    /// <summary>
    /// Gets whether the circuit breaker is allowing calls through.
    /// </summary>
    public System.Boolean IsCallAllowed
    {
        get
        {
            var currentState = State;

            return currentState switch
            {
                CircuitState.Closed => true,
                CircuitState.Open => ShouldAttemptReset(),
                CircuitState.HalfOpen => TryAcquireTestCall(),
                _ => false
            };
        }
    }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerState"/> class.
    /// </summary>
    /// <param name="options">The circuit breaker configuration options.</param>
    public CircuitBreakerState(Options.CircuitBreakerOptions options)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _options = options;
        _failureTimestamps = new System.Collections.Concurrent.ConcurrentQueue<System.DateTime>();
        _state = (System.Int32)CircuitState.Closed;
        _lastStateChangeTicks = System.DateTime.UtcNow.Ticks;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RecordSuccess()
    {
        var currentState = State;

        if (currentState == CircuitState.Closed)
        {
            // In Closed state, reset failure count on success
            System.Threading.Interlocked.Exchange(ref _failureCount, 0);
            System.Threading.Interlocked.Exchange(ref _consecutiveOpenCount, 0);
            CleanupOldFailures();
        }
        else if (currentState == CircuitState.HalfOpen)
        {
            // In HalfOpen, count successes towards closing
            System.Int32 successes = System.Threading.Interlocked.Increment(ref _successCount);

            if (successes >= _options.SuccessThreshold)
            {
                TransitionTo(CircuitState.Closed);
                System.Threading.Interlocked.Exchange(ref _failureCount, 0);
                System.Threading.Interlocked.Exchange(ref _successCount, 0);
                System.Threading.Interlocked.Exchange(ref _consecutiveOpenCount, 0);
                _failureTimestamps.Clear();
            }

            // Release test call flag
            System.Threading.Interlocked.Exchange(ref _testCallInProgress, 0);
        }
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RecordFailure()
    {
        var now = System.DateTime.UtcNow;
        _failureTimestamps.Enqueue(now);
        CleanupOldFailures();

        var currentState = State;

        if (currentState == CircuitState.HalfOpen)
        {
            // In HalfOpen, any failure reopens the circuit
            TransitionTo(CircuitState.Open);
            System.Threading.Interlocked.Exchange(ref _successCount, 0);
            System.Threading.Interlocked.Increment(ref _consecutiveOpenCount);
            System.Threading.Interlocked.Exchange(ref _testCallInProgress, 0);
        }
        else if (currentState == CircuitState.Closed)
        {
            System.Int32 failures = System.Threading.Interlocked.Increment(ref _failureCount);

            if (failures >= _options.FailureThreshold)
            {
                TransitionTo(CircuitState.Open);
                System.Threading.Interlocked.Increment(ref _consecutiveOpenCount);
            }
        }
    }

    /// <summary>
    /// Gets diagnostic information about the circuit breaker state.
    /// </summary>
    /// <returns>A string containing diagnostic information.</returns>
    public System.String GetDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        _ = sb.AppendLine($"Circuit Breaker State: {State}");
        _ = sb.AppendLine($"Failure Count: {FailureCount}");
        _ = sb.AppendLine($"Success Count: {SuccessCount}");
        _ = sb.AppendLine($"Last State Change: {LastStateChange:yyyy-MM-dd HH:mm:ss.fff} UTC");
        _ = sb.AppendLine($"Consecutive Opens: {System.Threading.Interlocked.Read(ref _consecutiveOpenCount)}");

        if (State == CircuitState.Open)
        {
            var timeSinceOpen = System.DateTime.UtcNow - LastStateChange;
            var currentOpenDuration = GetCurrentOpenDuration();
            _ = sb.AppendLine($"Time Since Open: {timeSinceOpen.TotalSeconds:N1}s");
            _ = sb.AppendLine($"Will Retry In: {(currentOpenDuration - timeSinceOpen).TotalSeconds:N1}s");
        }

        return sb.ToString();
    }

    #endregion Methods

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean ShouldAttemptReset()
    {
        var timeSinceStateChange = System.DateTime.UtcNow - LastStateChange;
        var currentOpenDuration = GetCurrentOpenDuration();

        if (timeSinceStateChange >= currentOpenDuration)
        {
            // Try to transition to HalfOpen
            var previousState = (CircuitState)System.Threading.Interlocked.CompareExchange(
                ref _state,
                (System.Int32)CircuitState.HalfOpen,
                (System.Int32)CircuitState.Open);

            if (previousState == CircuitState.Open)
            {
                // Successfully transitioned to HalfOpen
                System.Threading.Interlocked.Exchange(ref _lastStateChangeTicks, System.DateTime.UtcNow.Ticks);
                System.Threading.Interlocked.Exchange(ref _successCount, 0);
                System.Threading.Interlocked.Exchange(ref _failureCount, 0);

                if (_options.LogStateChanges)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Circuit Breaker: Open -> HalfOpen");
                }

                return true;
            }
        }

        return false;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Boolean TryAcquireTestCall()
    {
        // Only one test call at a time in HalfOpen state
        return System.Threading.Interlocked.CompareExchange(ref _testCallInProgress, 1, 0) == 0;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void TransitionTo(CircuitState newState)
    {
        var oldState = State;
        State = newState;
        System.Threading.Interlocked.Exchange(ref _lastStateChangeTicks, System.DateTime.UtcNow.Ticks);

        if (_options.LogStateChanges)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Circuit Breaker: {oldState} -> {newState}");
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CleanupOldFailures()
    {
        var cutoff = System.DateTime.UtcNow - _options.FailureWindow;

        while (_failureTimestamps.TryPeek(out var oldest) && oldest < cutoff)
        {
            _ = _failureTimestamps.TryDequeue(out _);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.TimeSpan GetCurrentOpenDuration()
    {
        if (!_options.UseExponentialBackoff)
        {
            return _options.OpenDuration;
        }

        // Calculate exponential backoff duration with overflow protection
        var openCount = System.Threading.Interlocked.Read(ref _consecutiveOpenCount);
        
        // Limit openCount to prevent overflow (2^20 is large enough for practical purposes)
        if (openCount > 20)
        {
            return _options.MaxOpenDuration;
        }

        var multiplier = System.Math.Pow(2, openCount - 1);
        
        // Check for potential overflow before multiplication
        var maxTicks = _options.MaxOpenDuration.Ticks;
        var baseTicks = _options.OpenDuration.Ticks;
        
        if (multiplier > maxTicks / (System.Double)baseTicks)
        {
            return _options.MaxOpenDuration;
        }

        var duration = System.TimeSpan.FromTicks((System.Int64)(baseTicks * multiplier));

        return duration > _options.MaxOpenDuration ? _options.MaxOpenDuration : duration;
    }

    #endregion Private Methods
}
