using System.Collections.Generic;

namespace Notio.Network.Http.Exceptions;

public class ValidationException : BaseException
{
    public ValidationException(string message) : base(message)
    {
    }

    public ValidationException(string message, IDictionary<string, string[]> details)
        : base(message, details)
    {
    }
}