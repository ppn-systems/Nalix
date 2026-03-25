// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides extension methods for <see cref="Task"/>
/// and <see cref="Task{TResult}"/>.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// <para>Suspends execution until the specified <see cref="Task"/> is completed.</para>
    /// <para>This method operates similarly to the <see langword="await"/> C# operator,
    /// but is meant to be called from a non-<see langword="async"/> method.</para>
    /// </summary>
    /// <param name="this">The <see cref="Task"/> on which this method is called.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Await(this Task @this)
    {
        System.ArgumentNullException.ThrowIfNull(@this);

        @this.GetAwaiter().GetResult();
    }

    /// <summary>
    /// <para>Suspends execution until the specified <see cref="Task"/> is completed
    /// and returns its result.</para>
    /// <para>This method operates similarly to the <see langword="await"/> C# operator,
    /// but is meant to be called from a non-<see langword="async"/> method.</para>
    /// </summary>
    /// <typeparam name="TResult">The type of the @this's result.</typeparam>
    /// <param name="this">The <see cref="Task{TResult}"/> on which this method is called.</param>
    /// <returns>The result of <paramref name="this"/>.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static TResult Await<TResult>(this Task<TResult> @this)
    {
        System.ArgumentNullException.ThrowIfNull(@this);

        return @this.GetAwaiter().GetResult();
    }

    /// <summary>
    /// <para>Suspends execution until the specified <see cref="Task"/> is completed.</para>
    /// <para>This method operates similarly to the <see langword="await" /> C# operator,
    /// but is meant to be called from a non-<see langword="async" /> method.</para>
    /// </summary>
    /// <param name="this">The <see cref="Task" /> on which this method is called.</param>
    /// <param name="continueOnCapturedContext">If set to <see langword="true"/>,
    /// attempts to marshal the continuation back to the original context captured.
    /// This parameter has the same effect as calling the <see cref="Task.ConfigureAwait(System.Boolean)"/>
    /// method.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Await(
        this Task @this,
        System.Boolean continueOnCapturedContext)
    {
        System.ArgumentNullException.ThrowIfNull(@this);

        @this.ConfigureAwait(continueOnCapturedContext).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Suspends execution until the specified <see cref="Task{TResult}"/> is completed or the timeout expires.
    /// <para>
    /// If the task completes within the specified timeout, its result is returned.
    /// Otherwise, <c>default</c> is returned.
    /// </para>
    /// <para>
    /// This method is useful for awaiting a task with a timeout in non-<see langword="async"/> methods.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <param name="this">The <see cref="Task{TResult}"/> to await.</param>
    /// <param name="msTimeout">The timeout in milliseconds.</param>
    /// <returns>
    /// The result of the task if it completes within the timeout; otherwise, <c>default</c>.
    /// </returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static async Task<T?> WithTimeout<T>(
        this Task<T> @this,
        System.Int32 msTimeout)
    {
        Task timeout = Task.Delay(msTimeout);
        Task completed = await Task.WhenAny(@this, timeout);

        if (completed == @this)
        {
            return await @this;
        }

        return default; // or throw TimeoutException
    }

    /// <summary>
    /// Registers cancellation such that the TCS will complete as canceled
    /// if the token fires. Auto-disposes on leaving using-scope.
    /// </summary>
    /// <typeparam name="T">TCS payload type.</typeparam>
    /// <param name="tcs">The TaskCompletionSource instance.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>IDisposable registration handle.</returns>
    public static System.IDisposable LinkCancellation<T>(
        this TaskCompletionSource<T> tcs, System.Threading.CancellationToken token) => !token.CanBeCanceled ? DummyDisposable.Instance : token.Register(() => tcs.TrySetCanceled(token));

    private sealed class DummyDisposable : System.IDisposable
    {
        public static readonly DummyDisposable Instance = new();
        private DummyDisposable() { }
        public void Dispose() { }
    }
}
