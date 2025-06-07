using System;

namespace Nalix.Assemblies;

/// <summary>
/// Structure containing comprehensive assembly version information.
/// </summary>
public readonly struct AssemblyInfo
{
    /// <summary>
    /// The product name associated with the assembly.
    /// </summary>
    public string Product { get; init; }

    /// <summary>
    /// The version of the assembly.
    /// </summary>
    public string Version { get; init; }

    /// <summary>
    /// The company name associated with the assembly.
    /// </summary>
    public string Company { get; init; }

    /// <summary>
    /// The copyright information associated with the assembly.
    /// </summary>
    public string Copyright { get; init; }

    /// <summary>
    /// The build time of the assembly.
    /// </summary>
    public DateTime BuildTime { get; init; }

    /// <summary>
    /// The file version of the assembly.
    /// </summary>
    public string FileVersion { get; init; }

    /// <summary>
    /// The name of the assembly.
    /// </summary>
    public string AssemblyName { get; init; }

    /// <summary>
    /// The informational version of the assembly.
    /// </summary>
    public string InformationalVersion { get; init; }
}
