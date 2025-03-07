using System;

namespace Notio.Shared.Configuration.Exceptions;

/// <summary>
/// Provides factory methods for creating <see cref="ConfiguredException"/> instances
/// related to configuration errors.
/// </summary>
public static class ConfiguredExceptionFactory
{
    // Error message templates
    private const string MissingValueMessage = "Required configuration value is missing.";
    private const string InvalidTypeMessage = "Invalid configuration value type (expected: {0}).";
    private const string OutOfRangeMessage = "Configuration value is out of the allowed range ({0} - {1}).";
    private const string FileAccessMessage = "Cannot access configuration file: {0}.";

    /// <summary>
    /// Creates an exception for a missing configuration value.
    /// </summary>
    /// <param name="section">The configuration section where the missing value is expected.</param>
    /// <param name="key">The specific configuration key that is missing.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException MissingValue(string section, string key)
        => new(MissingValueMessage, section, key);

    /// <summary>
    /// Creates an exception for a missing configuration value, specifying the configuration file.
    /// </summary>
    /// <param name="section">The configuration section where the missing value is expected.</param>
    /// <param name="key">The specific configuration key that is missing.</param>
    /// <param name="configFilePath">The path to the configuration file.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException MissingValue(string section, string key, string configFilePath)
        => new(MissingValueMessage, section, key, configFilePath: configFilePath);

    /// <summary>
    /// Creates an exception for an invalid configuration value type.
    /// </summary>
    /// <param name="section">The configuration section containing the invalid value.</param>
    /// <param name="key">The configuration key associated with the invalid value.</param>
    /// <param name="expectedType">The expected data type of the configuration value.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException InvalidType(string section, string key, Type expectedType)
        => new(string.Format(InvalidTypeMessage, expectedType.Name), section, key, expectedType);

    /// <summary>
    /// Creates an exception for an invalid configuration value type, specifying the configuration file.
    /// </summary>
    /// <param name="section">The configuration section containing the invalid value.</param>
    /// <param name="key">The configuration key associated with the invalid value.</param>
    /// <param name="expectedType">The expected data type of the configuration value.</param>
    /// <param name="configFilePath">The path to the configuration file.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException InvalidType(string section, string key, Type expectedType, string configFilePath)
        => new(string.Format(InvalidTypeMessage, expectedType.Name), section, key, expectedType, configFilePath);

    /// <summary>
    /// Creates an exception for an out-of-range configuration value.
    /// </summary>
    /// <param name="section">The configuration section containing the out-of-range value.</param>
    /// <param name="key">The configuration key associated with the value.</param>
    /// <param name="minValue">The minimum allowed value.</param>
    /// <param name="maxValue">The maximum allowed value.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException OutOfRange(string section, string key, IComparable minValue, IComparable maxValue)
        => new(string.Format(OutOfRangeMessage, minValue, maxValue), section, key);

    /// <summary>
    /// Creates an exception for an out-of-range configuration value, specifying the configuration file.
    /// </summary>
    /// <param name="section">The configuration section containing the out-of-range value.</param>
    /// <param name="key">The configuration key associated with the value.</param>
    /// <param name="minValue">The minimum allowed value.</param>
    /// <param name="maxValue">The maximum allowed value.</param>
    /// <param name="configFilePath">The path to the configuration file.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException OutOfRange(string section, string key, IComparable minValue, IComparable maxValue, string configFilePath)
        => new(string.Format(OutOfRangeMessage, minValue, maxValue), section, key, configFilePath: configFilePath);

    /// <summary>
    /// Creates an exception for a configuration file access error.
    /// </summary>
    /// <param name="configFilePath">The path to the configuration file that could not be accessed.</param>
    /// <param name="innerException">The exception that caused the file access error.</param>
    /// <returns>A new <see cref="ConfiguredException"/> instance.</returns>
    public static ConfiguredException FileAccessError(string configFilePath, Exception innerException)
        => new(string.Format(FileAccessMessage, configFilePath), innerException);
}
