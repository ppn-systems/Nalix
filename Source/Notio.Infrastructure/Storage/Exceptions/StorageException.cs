using System;

namespace Notio.Infrastructure.Storage.Exceptions;

public class StorageException : Exception
{
    public StorageException(string message) : base(message)
    {
    }

    public StorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}