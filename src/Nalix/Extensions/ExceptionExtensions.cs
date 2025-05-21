using Nalix.Common.Exceptions;
using Nalix.Exceptions;

namespace Nalix.Extensions;

/// <summary>
/// Provides extension methods for <see cref="System.Exception"/>.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Returns a value that tells whether an <see cref="System.Exception"/> is of a type that
    /// we better not catch and ignore.
    /// </summary>
    /// <param name="this">The exception being thrown.</param>
    /// <returns><see langword="true"/> if <paramref name="this"/> is a critical exception;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsCriticalException(this System.Exception @this)
        => @this.IsCriticalExceptionCore()
        || (@this.InnerException?.IsCriticalException() ?? false)
        || @this is System.AggregateException aggregateException
        && System.Linq.Enumerable.Any(aggregateException.InnerExceptions, e => e.IsCriticalExceptionCore());

    /// <summary>
    /// Returns a value that tells whether an <see cref="System.Exception"/> is of a type that
    /// will likely cause application failure.
    /// </summary>
    /// <param name="this">The exception being thrown.</param>
    /// <returns><see langword="true"/> if <paramref name="this"/> is a fatal exception;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsFatalException(this System.Exception @this)
        => @this.IsFatalExceptionCore()
        || (@this.InnerException?.IsFatalException() ?? false)
        || @this is System.AggregateException aggregateException
        && System.Linq.Enumerable.Any(aggregateException.InnerExceptions, e => e.IsCriticalExceptionCore());

    /// <summary>
    /// <para>Rethrows an already-thrown exception, preserving the stack trace of the original throw.</para>
    /// <para>This method does not return; its return type is an exception type so it can be used
    /// with <c>throw</c> semantics, e.g.: <c>throw ex.RethrowPreservingStackTrace();</c>,
    /// to let static code analysis tools that it throws instead of returning.</para>
    /// </summary>
    /// <param name="this">The exception to rethrow.</param>
    /// <returns>This method should never return; if it does, it is an indication of an internal error,
    /// so it returns an instance of <see cref="InternalErrorException"/>.</returns>
    public static InternalErrorException RethrowPreservingStackTrace(this System.Exception @this)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(@this).Throw();
        return SelfCheck.Failure("Reached unreachable code.");
    }

    private static bool IsCriticalExceptionCore(this System.Exception @this)
        => @this.IsFatalExceptionCore()
        || @this is System.AppDomainUnloadedException
        || @this is System.BadImageFormatException
        || @this is System.CannotUnloadAppDomainException
        || @this is System.InvalidProgramException
        || @this is System.NullReferenceException;

    private static bool IsFatalExceptionCore(this System.Exception @this)
        => @this is System.StackOverflowException
        || @this is System.OutOfMemoryException
        || @this is System.Threading.ThreadAbortException
        || @this is System.AccessViolationException;
}
