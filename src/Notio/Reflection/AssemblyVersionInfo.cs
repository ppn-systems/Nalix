using System;

namespace Notio.Reflection;

/// <summary>
/// Structure containing comprehensive assembly version information.
/// </summary>
public struct AssemblyVersionInfo
{
    /// <summary>
    /// The name of the assembly.
    /// </summary>
    public string AssemblyName { get; internal set; }

    /// <summary>
    /// The version of the assembly.
    /// </summary>
    public string Version { get; internal set; }

    /// <summary>
    /// The file version of the assembly.
    /// </summary>
    public string FileVersion { get; internal set; }

    /// <summary>
    /// The informational version of the assembly.
    /// </summary>
    public string InformationalVersion { get; internal set; }

    /// <summary>
    /// The company name associated with the assembly.
    /// </summary>
    public string Company { get; internal set; }

    /// <summary>
    /// The product name associated with the assembly.
    /// </summary>
    public string Product { get; internal set; }

    /// <summary>
    /// The copyright information associated with the assembly.
    /// </summary>
    public string Copyright { get; internal set; }

    /// <summary>
    /// The build time of the assembly.
    /// </summary>
    public DateTime BuildTime { get; internal set; }
}
