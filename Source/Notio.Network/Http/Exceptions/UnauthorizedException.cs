using System.Collections.Generic;

namespace Notio.Network.Http.Exceptions;

public class UnauthorizedException : BaseException
{
    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, IDictionary<string, string[]> details)
        : base(message, details)
    {
    }
}