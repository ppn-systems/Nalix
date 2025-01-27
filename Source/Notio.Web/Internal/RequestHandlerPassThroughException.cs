using System;

namespace Notio.Web.Internal
{
    // This exception is only created and handled internally,
    // so it doesn't need all the standard the bells and whistles.

    internal class RequestHandlerPassThroughException : Exception
    {
    }
}