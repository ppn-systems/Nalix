using System;

namespace Notio.Common.Exceptions;

public class SecurityException : BaseException
{
    public SecurityException(string message) : base(message)
    {
    }

    public SecurityException(string message, string paramName)
        : base($"{message} Parameter: {paramName}")
    {
    }

    public SecurityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}