using System;
using System.Globalization;
using System.Reflection;

namespace Notio.Helpers;

/// <summary>
/// Helper class for tasks related to Assembly.
/// </summary>
public static class AssemblyHelper
{
    /// <summary>
    /// Returns the version of the calling assembly as a string.
    /// </summary>
    /// <returns>
    /// The version of the calling assembly, or "Unknown Version" if the version is not available.
    /// </returns>
    public static string GetAssemblyVersion()
    {
        Assembly assembly = Assembly.GetCallingAssembly();
        Version version = assembly.GetName().Version;

        return version?.ToString() ?? "Unknown Version";
    }

    /// <summary>
    /// Returns the informational version of the calling assembly as a string.
    /// The informational version is retrieved from the <see cref="AssemblyInformationalVersionAttribute"/> attribute.
    /// </summary>
    /// <returns>
    /// The informational version of the calling assembly, or an empty string if the attribute is not defined.
    /// </returns>
    public static string GetAssemblyInformationalVersion()
    {
        var assembly = Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute?.InformationalVersion == null)
            return string.Empty;

        return attribute.InformationalVersion.Split('+')[0];
    }

    /// <summary>
    /// Parses the build time of the calling assembly based on the informational version string from the <see cref="AssemblyInformationalVersionAttribute"/> attribute.
    /// </summary>
    /// <param name="prefix">The prefix string to locate the build time (e.g., "+build").</param>
    /// <param name="format">The date-time format of the build time (default: "yyyyMMddHHmmss").</param>
    /// <returns>
    /// The build time as a <see cref="DateTime"/> object, or <c>default</c> if parsing fails.
    /// </returns>
    public static DateTime ParseAssemblyBuildTime(string prefix = "+build", string format = "yyyyMMddHHmmss")
    {
        var assembly = Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute?.InformationalVersion == null)
            return default;

        int buildTimeIndex = attribute.InformationalVersion.IndexOf(prefix, StringComparison.Ordinal);
        if (buildTimeIndex == -1)
            return default;

        string buildTimeString = attribute.InformationalVersion[(buildTimeIndex + prefix.Length)..];
        if (!DateTime.TryParseExact(buildTimeString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var buildTime))
            return default;

        return buildTime;
    }
}
