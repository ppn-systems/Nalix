using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Nalix.Runtime.Assemblies;

/// <summary>
/// High-performance helper class for retrieving assembly metadata information.
/// </summary>
public static class AssemblyInspector
{
    private static readonly Lazy<AssemblyInfo> LazyVersionInfo = new(() => GetVersionInfoInternal());

    /// <summary>
    /// Gets the version information of the assembly.
    /// </summary>
    public static AssemblyInfo VersionInfo => LazyVersionInfo.Value;

    /// <summary>
    /// Gets the assembly version.
    /// </summary>
    /// <returns>The assembly version.</returns>
    public static string GetAssemblyVersion() => VersionInfo.Version;

    /// <summary>
    /// Gets the informational version of the assembly.
    /// </summary>
    /// <returns>The informational version of the assembly.</returns>
    public static string GetAssemblyInformationalVersion() => VersionInfo.InformationalVersion;

    /// <summary>
    /// Gets the file version of the assembly.
    /// </summary>
    /// <returns>The file version of the assembly.</returns>
    public static string GetAssemblyFileVersion() => VersionInfo.FileVersion;

    /// <summary>
    /// Gets the company name associated with the assembly.
    /// </summary>
    /// <returns>The company name associated with the assembly.</returns>
    public static string GetAssemblyCompany() => VersionInfo.Company;

    /// <summary>
    /// Gets the product name associated with the assembly.
    /// </summary>
    /// <returns>The product name associated with the assembly.</returns>
    public static string GetAssemblyProduct() => VersionInfo.Product;

    /// <summary>
    /// Gets the copyright information associated with the assembly.
    /// </summary>
    /// <returns>The copyright information associated with the assembly.</returns>
    public static string GetAssemblyCopyright() => VersionInfo.Copyright;

    /// <summary>
    /// Gets the name of the assembly.
    /// </summary>
    /// <returns>The name of the assembly.</returns>
    public static string GetAssemblyName() => VersionInfo.AssemblyName;

    /// <summary>
    /// Gets the build time of the assembly.
    /// </summary>
    /// <returns>The build time of the assembly.</returns>
    public static DateTime GetAssemblyBuildTime() => VersionInfo.BuildTime;

    private static AssemblyInfo GetVersionInfoInternal()
    {
        Assembly assembly = Assembly.GetCallingAssembly();
        AssemblyName name = assembly.GetName();

        return new AssemblyInfo
        {
            AssemblyName = name.Name ?? "Unknown",
            Version = name.Version?.ToString() ?? "Unknown",
            FileVersion = GetAttribute<AssemblyFileVersionAttribute>(assembly)?.Version ?? "Unknown",
            InformationalVersion = ParseInformationalVersion(
                GetAttribute<AssemblyInformationalVersionAttribute>(assembly)),
            Company = GetAttribute<AssemblyCompanyAttribute>(assembly)?.Company ?? "Unknown",
            Product = GetAttribute<AssemblyProductAttribute>(assembly)?.Product ?? "Unknown",
            Copyright = GetAttribute<AssemblyCopyrightAttribute>(assembly)?.Copyright ?? "Unknown",
            BuildTime = ParseBuildTime(GetAttribute<AssemblyInformationalVersionAttribute>(assembly))
        };
    }

    private static T GetAttribute<T>(Assembly assembly) where T : Attribute =>
        assembly.GetCustomAttribute<T>();

    private static string ParseInformationalVersion(AssemblyInformationalVersionAttribute attr) =>
        attr?.InformationalVersion?.Split('+')[0] ?? "Unknown";

    private static DateTime ParseBuildTime(
        AssemblyInformationalVersionAttribute attr,
        string prefix = "+build", string format = "yyyyMMddHHmmss")
    {
        if (attr?.InformationalVersion is not { } version)
            return DateTime.MinValue;

        int index = version.IndexOf(prefix, StringComparison.Ordinal);
        if (index == -1)
            return DateTime.MinValue;

        string buildTimeStr = version[(index + prefix.Length)..];
        buildTimeStr = new string([.. buildTimeStr.TakeWhile(char.IsDigit)]);

        return DateTime.TryParseExact(
            buildTimeStr, format, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var buildTime) ? buildTime : DateTime.MinValue;
    }
}
