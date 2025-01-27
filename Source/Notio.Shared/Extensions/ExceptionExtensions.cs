using Notio.Common.Exceptions;
using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Notio.Shared.Extensions;

public static class ExceptionExtensions
{
    public static bool IsCriticalException(this Exception @this)
    {
        if (!@this.IsCriticalExceptionCore())
        {
            Exception? innerException = @this.InnerException;
            if (innerException == null || !innerException.IsCriticalException())
            {
                if (@this is AggregateException ex)
                {
                    return ex.InnerExceptions.Any((e) => e.IsCriticalException());
                }

                return false;
            }
        }

        return true;
    }

    public static bool IsFatalException(this Exception @this)
    {
        if (!@this.IsFatalExceptionCore())
        {
            Exception? innerException = @this.InnerException;
            if (innerException == null || !innerException.IsFatalException())
            {
                if (@this is AggregateException ex)
                {
                    return ex.InnerExceptions.Any((e) => e.IsFatalException());
                }

                return false;
            }
        }

        return true;
    }

    public static InternalErrorException RethrowPreservingStackTrace(this Exception @this)
    {
        ExceptionDispatchInfo.Capture(@this).Throw();
        return SelfCheck.Failure("Reached unreachable code.", "C:\\Notio\\Source\\Notio.Shared.Extensions\\ExceptionExtensions.cs", 63);
    }

    private static bool IsCriticalExceptionCore(this Exception @this) =>
        @this.IsFatalExceptionCore() ||
        @this is AppDomainUnloadedException or BadImageFormatException or
        CannotUnloadAppDomainException or InvalidProgramException or
        NullReferenceException;

    private static bool IsFatalExceptionCore(this Exception @this) =>
        @this is AccessViolationException ||
        @this is not (StackOverflowException or OutOfMemoryException or ThreadAbortException);
}