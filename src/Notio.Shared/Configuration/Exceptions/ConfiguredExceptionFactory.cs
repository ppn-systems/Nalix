using System;

namespace Notio.Shared.Configuration.Exceptions;

public static class ConfiguredExceptionFactory
{
    /// <summary>
    /// Creates a new <see cref="ConfiguredException"/> for a missing configuration value.
    /// </summary>
    /// <param name="section">The configuration section.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="configFilePath">Optional. The path to the configuration file.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException MissingValue(string section, string key, string? configFilePath = null)
        => new("Required configuration value is missing",
                section, key, configFilePath: configFilePath);


    /// <summary>
    /// Creates a new <see cref="ConfiguredException"/> for an invalid configuration value type.
    /// </summary>
    /// <param name="section">The configuration section.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="expectedType">The expected type of the configuration value.</param>
    /// <param name="configFilePath">Optional. The path to the configuration file.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException InvalidType(
        string section, string key,
        Type expectedType,
        string? configFilePath = null)
        => new($"Invalid configuration value type (expected: {expectedType.Name})",
                section, key, expectedType, configFilePath);


    /// <summary>
    /// Creates a new <see cref="ConfiguredException"/> for an out-of-range configuration value.
    /// </summary>
    /// <param name="section">The configuration section.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="minValue">The minimum allowed value.</param>
    /// <param name="maxValue">The maximum allowed value.</param>
    /// <param name="configFilePath">Optional. The path to the configuration file.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException OutOfRange(
        string section, string key,
        object minValue, object maxValue,
        string? configFilePath = null)
        => new($"Configuration value is out of the allowed range ({minValue} - {maxValue})",
                section, key, configFilePath: configFilePath);

    /// <summary>
    /// Creates a new <see cref="ConfiguredException"/> for a configuration file access error.
    /// </summary>
    /// <param name="configFilePath">The path to the configuration file.</param>
    /// <param name="innerException">The exception that caused the access error.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException FileAccessError(string configFilePath, Exception innerException)
        => new($"Cannot access configuration file: {configFilePath}", innerException);
}
