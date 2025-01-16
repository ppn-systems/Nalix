using System;

namespace Notio.Http.Exceptions;

/// <summary>
/// An exception that is thrown when an HTTP response could not be parsed to a particular format.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ParsingException"/> class.
/// </remarks>
/// <param name="call">Details of the HTTP call that caused the exception.</param>
/// <param name="expectedFormat">The format that could not be parsed to, i.e. JSON.</param>
/// <param name="inner">The inner exception.</param>
public class ParsingException(NotioCall call, string expectedFormat, Exception inner)
    : HttpException(call, BuildMessage(call, expectedFormat), inner)
{
    /// <summary>
    /// The format that could not be parsed to, i.e. JSON.
    /// </summary>
    public string ExpectedFormat { get; } = expectedFormat;

    private static string BuildMessage(NotioCall call, string expectedFormat)
    {
        var msg = $"Response could not be deserialized to {expectedFormat}";
        return msg + (call == null ? "." : $": {call}");
    }
}