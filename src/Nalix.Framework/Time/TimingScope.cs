// Copyright (c) 2026 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Time;

/// <summary>
/// Represents a lightweight, allocation-free scope for measuring elapsed time
/// using a monotonic, high-resolution clock.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TimingScope"/> is designed for high-performance scenarios where
/// minimal overhead is required, such as networking, IO pipelines, and
/// infrastructure-level code.
/// </para>
/// <para>
/// This type is implemented as a <c>readonly struct</c> to ensure it is
/// stack-allocated and free of heap allocations. It relies on a monotonic
/// clock source and is not affected by system clock changes.
/// </para>
/// </remarks>
/// <threadsafety>
/// This type is thread-safe as it is immutable after creation.
/// </threadsafety>
public readonly struct TimingScope
{
    #region Fields

    /// <summary>
    /// The starting timestamp, expressed in monotonic clock ticks.
    /// </summary>
    private readonly System.Int64 _t0;

    #endregion Fields

    #region Private Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TimingScope"/> struct
    /// using the specified start timestamp.
    /// </summary>
    /// <param name="startTicks">
    /// The starting timestamp, expressed in monotonic clock ticks.
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private TimingScope(System.Int64 startTicks) => _t0 = startTicks;

    #endregion Private Constructors

    #region Properties

    /// <summary>
    /// Gets the elapsed time, expressed in monotonic clock ticks,
    /// since the timing scope was started.
    /// </summary>
    public System.Int64 ElapsedTicks => Clock.MonoTicksNow() - _t0;

    #endregion Properties

    #region APIs

    /// <summary>
    /// Starts a new <see cref="TimingScope"/> instance.
    /// </summary>
    /// <returns>
    /// A <see cref="TimingScope"/> that can be used to measure elapsed time.
    /// </returns>
    /// <remarks>
    /// This method captures the current timestamp from the monotonic clock
    /// and should be called as close as possible to the beginning of the
    /// measured operation.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TimingScope Start() => new(Clock.MonoTicksNow());

    /// <summary>
    /// Stops the timing scope and returns the elapsed time in milliseconds.
    /// </summary>
    /// <returns>
    /// The elapsed time, in milliseconds, since the timing scope was started.
    /// </returns>
    /// <remarks>
    /// This method does not modify the state of the instance and may be called
    /// multiple times to retrieve updated elapsed values.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Double GetElapsedMilliseconds() => Clock.MonoTicksToMilliseconds(Clock.MonoTicksNow() - _t0);

    #endregion APIs
}