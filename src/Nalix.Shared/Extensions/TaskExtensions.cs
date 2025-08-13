namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides extension methods for <see cref="System.Threading.Tasks.Task"/>
/// and <see cref="System.Threading.Tasks.Task{TResult}"/>.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// <para>Suspends execution until the specified <see cref="System.Threading.Tasks.Task"/> is completed.</para>
    /// <para>This method operates similarly to the <see langword="await"/> C# operator,
    /// but is meant to be called from a non-<see langword="async"/> method.</para>
    /// </summary>
    /// <param name="this">The <see cref="System.Threading.Tasks.Task"/> on which this method is called.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Await(this System.Threading.Tasks.Task @this)
    {
        System.ArgumentNullException.ThrowIfNull(@this);

        @this.GetAwaiter().GetResult();
    }

    /// <summary>
    /// <para>Suspends execution until the specified <see cref="System.Threading.Tasks.Task"/> is completed
    /// and returns its result.</para>
    /// <para>This method operates similarly to the <see langword="await"/> C# operator,
    /// but is meant to be called from a non-<see langword="async"/> method.</para>
    /// </summary>
    /// <typeparam name="TResult">The type of the @this's result.</typeparam>
    /// <param name="this">The <see cref="System.Threading.Tasks.Task{TResult}"/> on which this method is called.</param>
    /// <returns>The result of <paramref name="this"/>.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static TResult Await<TResult>(this System.Threading.Tasks.Task<TResult> @this)
    {
        System.ArgumentNullException.ThrowIfNull(@this);

        return @this.GetAwaiter().GetResult();
    }

    /// <summary>
    /// <para>Suspends execution until the specified <see cref="System.Threading.Tasks.Task"/> is completed.</para>
    /// <para>This method operates similarly to the <see langword="await" /> C# operator,
    /// but is meant to be called from a non-<see langword="async" /> method.</para>
    /// </summary>
    /// <param name="this">The <see cref="System.Threading.Tasks.Task" /> on which this method is called.</param>
    /// <param name="continueOnCapturedContext">If set to <see langword="true"/>,
    /// attempts to marshal the continuation back to the original context captured.
    /// This parameter has the same effect as calling the <see cref="System.Threading.Tasks.Task.ConfigureAwait(System.Boolean)"/>
    /// method.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Await(
        this System.Threading.Tasks.Task @this,
        System.Boolean continueOnCapturedContext)
    {
        System.ArgumentNullException.ThrowIfNull(@this);

        @this.ConfigureAwait(continueOnCapturedContext).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Suspends execution until the specified <see cref="System.Threading.Tasks.Task{TResult}"/> is completed or the timeout expires.
    /// <para>
    /// If the task completes within the specified timeout, its result is returned.
    /// Otherwise, <c>default</c> is returned.
    /// </para>
    /// <para>
    /// This method is useful for awaiting a task with a timeout in non-<see langword="async"/> methods.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the task.</typeparam>
    /// <param name="this">The <see cref="System.Threading.Tasks.Task{TResult}"/> to await.</param>
    /// <param name="msTimeout">The timeout in milliseconds.</param>
    /// <returns>
    /// The result of the task if it completes within the timeout; otherwise, <c>default</c>.
    /// </returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static async System.Threading.Tasks.Task<T?> WithTimeout<T>(
        this System.Threading.Tasks.Task<T> @this,
        System.Int32 msTimeout)
    {
        var timeout = System.Threading.Tasks.Task.Delay(msTimeout);
        var completed = await System.Threading.Tasks.Task.WhenAny(@this, timeout);

        if (completed == @this)
        {
            return await @this;
        }

        return default; // or throw TimeoutException
    }
}
