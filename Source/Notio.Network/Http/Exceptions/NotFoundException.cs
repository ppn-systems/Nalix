using System.Collections.Generic;

namespace Notio.Network.Http.Exceptions;

public class NotFoundException : BaseException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, IDictionary<string, string[]> details)
        : base(message, details)
    {
    }
}