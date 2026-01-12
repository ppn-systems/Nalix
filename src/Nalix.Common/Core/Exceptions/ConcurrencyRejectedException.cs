namespace Nalix.Common.Core.Exceptions;

/// <summary>
/// Exception thrown when a concurrency conflict is detected and the operation is rejected.
/// </summary>
public sealed class ConcurrencyRejectedException : InternalErrorException
{
    /// <inheritdoc/>
    public ConcurrencyRejectedException() : base()
    {
    }

    /// <inheritdoc/>
    public ConcurrencyRejectedException(System.String message) : base(message)
    {
    }

    /// <inheritdoc/>
    public ConcurrencyRejectedException(System.String message, System.String details) : base(message, details)
    {
    }

    /// <inheritdoc/>
    public ConcurrencyRejectedException(System.String message, System.Exception innerException) : base(message, innerException)
    {
    }
}
