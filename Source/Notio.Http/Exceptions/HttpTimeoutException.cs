using System;

namespace Notio.Http.Exceptions;

/// <summary>
/// An exception that is thrown when an HTTP call made by Flurl.Http times out.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpTimeoutException"/> class.
/// </remarks>
/// <param name="call">Details of the HTTP call that caused the exception.</param>
/// <param name="inner">The inner exception.</param>
public class HttpTimeoutException(NotioCall call, Exception inner) : HttpException(call, BuildMessage(call), inner)
{
    private static string BuildMessage(NotioCall call) =>
        call == null ? "Call timed out." : $"Call timed out: {call}";
}