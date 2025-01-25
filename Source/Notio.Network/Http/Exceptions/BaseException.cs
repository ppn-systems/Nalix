using System;
using System.Collections.Generic;

namespace Notio.Network.Http.Exceptions;

public abstract class BaseException : Exception
{
    public IDictionary<string, string[]> Details { get; }

    protected BaseException(string message) : base(message)
    {
        Details = new Dictionary<string, string[]>();
    }

    protected BaseException(string message, IDictionary<string, string[]> details) : base(message)
    {
        Details = details;
    }

    protected BaseException(string message, Exception innerException) : base(message, innerException)
    {
        Details = new Dictionary<string, string[]>();
    }
}