namespace Nalix.Assemblies;

/// <summary>
/// High-performance helper class for retrieving assembly metadata information.
/// </summary>
public static class AssemblyInspector
{
    private static readonly System.Lazy<AssemblyInfo> LazyVersionInfo = new(() => GetVersionInfoInternal());

    /// <summary>
    /// Gets the version information of the assembly.
    /// </summary>
    public static AssemblyInfo VersionInfo => LazyVersionInfo.Value;

    /// <summary>
    /// Gets the assembly version.
    /// </summary>
    /// <returns>The assembly version.</returns>
    public static System.String GetAssemblyVersion() => VersionInfo.Version;

    /// <summary>
    /// Gets the informational version of the assembly.
    /// </summary>
    /// <returns>The informational version of the assembly.</returns>
    public static System.String GetAssemblyInformationalVersion() => VersionInfo.InformationalVersion;

    /// <summary>
    /// Gets the file version of the assembly.
    /// </summary>
    /// <returns>The file version of the assembly.</returns>
    public static System.String GetAssemblyFileVersion() => VersionInfo.FileVersion;

    /// <summary>
    /// Gets the company name associated with the assembly.
    /// </summary>
    /// <returns>The company name associated with the assembly.</returns>
    public static System.String GetAssemblyCompany() => VersionInfo.Company;

    /// <summary>
    /// Gets the product name associated with the assembly.
    /// </summary>
    /// <returns>The product name associated with the assembly.</returns>
    public static System.String GetAssemblyProduct() => VersionInfo.Product;

    /// <summary>
    /// Gets the copyright information associated with the assembly.
    /// </summary>
    /// <returns>The copyright information associated with the assembly.</returns>
    public static System.String GetAssemblyCopyright() => VersionInfo.Copyright;

    /// <summary>
    /// Gets the name of the assembly.
    /// </summary>
    /// <returns>The name of the assembly.</returns>
    public static System.String GetAssemblyName() => VersionInfo.AssemblyName;

    /// <summary>
    /// Gets the build time of the assembly.
    /// </summary>
    /// <returns>The build time of the assembly.</returns>
    public static System.DateTime GetAssemblyBuildTime() => VersionInfo.BuildTime;

    private static AssemblyInfo GetVersionInfoInternal()
    {
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetCallingAssembly();
        System.Reflection.AssemblyName name = assembly.GetName();

        return new AssemblyInfo
        {
            AssemblyName = name.Name ?? "Unknown",
            Version = name.Version?.ToString() ?? "Unknown",
            FileVersion = GetAttribute<System.Reflection.AssemblyFileVersionAttribute>(assembly)?.Version ?? "Unknown",
            InformationalVersion = ParseInformationalVersion(
                GetAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly)),
            Company = GetAttribute<System.Reflection.AssemblyCompanyAttribute>(assembly)?.Company ?? "Unknown",
            Product = GetAttribute<System.Reflection.AssemblyProductAttribute>(assembly)?.Product ?? "Unknown",
            Copyright = GetAttribute<System.Reflection.AssemblyCopyrightAttribute>(assembly)?.Copyright ?? "Unknown",
            BuildTime = ParseBuildTime(GetAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly))
        };
    }

    private static T GetAttribute<T>(
        System.Reflection.Assembly assembly) where T : System.Attribute
        => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<T>(assembly);

    private static System.String ParseInformationalVersion(
        System.Reflection.AssemblyInformationalVersionAttribute attr) =>
        attr?.InformationalVersion?.Split('+')[0] ?? "Unknown";

    private static System.DateTime ParseBuildTime(
        System.Reflection.AssemblyInformationalVersionAttribute attr,
        System.String prefix = "+build", System.String format = "yyyyMMddHHmmss")
    {
        if (attr?.InformationalVersion is not { } version)
            return System.DateTime.MinValue;

        System.Int32 index = version.IndexOf(prefix, System.StringComparison.Ordinal);
        if (index == -1)
            return System.DateTime.MinValue;

        System.String buildTimeStr = version[(index + prefix.Length)..];
        buildTimeStr = new System.String([.. System.Linq.Enumerable.TakeWhile(buildTimeStr, char.IsDigit)]);

        return System.DateTime.TryParseExact(
            buildTimeStr, format, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var buildTime) ? buildTime : System.DateTime.MinValue;
    }
}