using System;

namespace Notio.Shared.Configuration;

/// <summary>
/// Represents errors that occur during the configuration process.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConfiguredException"/> class with a specified error message.
/// </remarks>
/// <param name="message">The error message that describes the exception.</param>
public class ConfiguredException(string message) : Exception(message)
{
}