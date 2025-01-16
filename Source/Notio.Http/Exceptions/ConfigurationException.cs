using System;

namespace Notio.Http.Exceptions;

/// <summary>
/// An exception that is thrown when Flurl.Http has been misconfigured.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConfigurationException"/> class.
/// </remarks>
public class ConfigurationException(string message) : Exception(message)
{
}