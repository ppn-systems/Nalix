namespace Nalix.Runtime;

/// <summary>
/// Provides information about the platform and runtime environment.
/// </summary>
public static class PlatformInfo
{
    /// <summary>
    /// Gets a value indicating whether the operating system is 64-bit.
    /// </summary>
    public static bool Is64Bit => System.Environment.Is64BitOperatingSystem;

    /// <summary>
    /// Gets the version of the .NET runtime.
    /// </summary>
    public static string DotNetVersion => System.Environment.Version.ToString();

    /// <summary>
    /// Gets the description of the operating system.
    /// </summary>
    public static string OSDescription => System.Runtime.InteropServices.RuntimeInformation.OSDescription;
}
