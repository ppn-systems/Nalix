using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Notio.Reflection;

/// <summary>
/// High-performance helper class for retrieving assembly metadata information.
/// </summary>
public static class AssemblyMetadata
{
    /// <summary>
    /// Returns the version of the calling assembly as a string.
    /// </summary>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// The version of the assembly, or "Unknown Version" if the version is not available.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetAssemblyVersion(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        Version version = assembly.GetName().Version;

        return version?.ToString() ?? "Unknown Version";
    }

    /// <summary>
    /// Returns the informational version of the calling assembly as a string.
    /// The informational version is retrieved from the <see cref="AssemblyInformationalVersionAttribute"/> attribute.
    /// </summary>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// The informational version of the assembly, or an empty string if the attribute is not defined.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetAssemblyInformationalVersion(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute?.InformationalVersion == null)
            return string.Empty;

        // Find the first '+' character, which typically separates the version from build metadata
        int plusIndex = attribute.InformationalVersion.IndexOf('+');
        return plusIndex >= 0
            ? attribute.InformationalVersion[..plusIndex]
            : attribute.InformationalVersion;
    }

    /// <summary>
    /// Parses the build time of the calling assembly based on the informational version string from the <see cref="AssemblyInformationalVersionAttribute"/> attribute.
    /// </summary>
    /// <param name="prefix">The prefix string to locate the build time (e.g., "+build").</param>
    /// <param name="format">The date-time format of the build time (default: "yyyyMMddHHmmss").</param>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// The build time as a <see cref="DateTime"/> object, or <c>DateTime.MinValue</c> if parsing fails.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime ParseAssemblyBuildTime(string prefix = "+build", string format = "yyyyMMddHHmmss", Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute?.InformationalVersion == null)
            return DateTime.MinValue;

        int buildTimeIndex = attribute.InformationalVersion.IndexOf(prefix, StringComparison.Ordinal);
        if (buildTimeIndex == -1)
            return DateTime.MinValue;

        // Extract the build time substring
        string buildTimeString = attribute.InformationalVersion.AsSpan()[(buildTimeIndex + prefix.Length)..].ToString();

        // Trim any non-numeric characters that might follow the timestamp
        int endIndex = 0;
        while (endIndex < buildTimeString.Length && char.IsDigit(buildTimeString[endIndex]))
            endIndex++;

        if (endIndex < format.Length)
            return DateTime.MinValue; // Not enough digits for the format

        buildTimeString = buildTimeString[..Math.Min(endIndex, format.Length)];

        if (!DateTime.TryParseExact(buildTimeString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var buildTime))
            return DateTime.MinValue;

        return buildTime;
    }

    /// <summary>
    /// Gets the file version of the calling assembly.
    /// </summary>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// The file version of the assembly, or an empty string if the attribute is not defined.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetAssemblyFileVersion(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
        return attribute?.Version ?? string.Empty;
    }

    /// <summary>
    /// Gets the company name attribute of the calling assembly.
    /// </summary>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// The company name attribute of the assembly, or an empty string if the attribute is not defined.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetAssemblyCompany(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        return attribute?.Company ?? string.Empty;
    }

    /// <summary>
    /// Gets the product name attribute of the calling assembly.
    /// </summary>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// The product name attribute of the assembly, or an empty string if the attribute is not defined.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetAssemblyProduct(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
        return attribute?.Product ?? string.Empty;
    }

    /// <summary>
    /// Gets the copyright attribute of the calling assembly.
    /// </summary>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// The copyright attribute of the assembly, or an empty string if the attribute is not defined.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetAssemblyCopyright(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
        return attribute?.Copyright ?? string.Empty;
    }

    /// <summary>
    /// Gets the name of the calling assembly.
    /// </summary>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// The name of the assembly.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetAssemblyName(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        return assembly.GetName().Name;
    }

    /// <summary>
    /// Gets the full versioning information of the calling assembly.
    /// </summary>
    /// <param name="assembly">Optional specific assembly. If null, uses the calling assembly.</param>
    /// <returns>
    /// A structured object containing all versioning information.
    /// </returns>
    public static AssemblyVersionInfo GetVersionInfo(Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        return new AssemblyVersionInfo
        {
            AssemblyName = assembly.GetName().Name,
            Version = assembly.GetName().Version?.ToString() ?? string.Empty,
            FileVersion = GetAssemblyFileVersion(assembly),
            InformationalVersion = GetAssemblyInformationalVersion(assembly),
            Company = GetAssemblyCompany(assembly),
            Product = GetAssemblyProduct(assembly),
            Copyright = GetAssemblyCopyright(assembly),
            BuildTime = ParseAssemblyBuildTime(assembly: assembly)
        };
    }
}
