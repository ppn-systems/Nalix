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
    /// <typeparam name="TResult">The type of the task's result.</typeparam>
    /// <param name="this">The <see cref="System.Threading.Tasks.Task{TResult}"/> on which this method is called.</param>
    /// <returns>The result of <paramref name="this"/>.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="this"/> is <see langword="null"/>.</exception>
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
    public static void Await(
        this System.Threading.Tasks.Task @this,
        System.Boolean continueOnCapturedContext)
    {
        System.ArgumentNullException.ThrowIfNull(@this);

        @this.ConfigureAwait(continueOnCapturedContext).GetAwaiter().GetResult();
    }
}
