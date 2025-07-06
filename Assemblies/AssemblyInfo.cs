namespace Nalix.Assemblies;

/// <summary>
/// Structure containing comprehensive assembly version information.
/// </summary>
public readonly struct AssemblyInfo
{
    /// <summary>
    /// The product name associated with the assembly.
    /// </summary>
    public System.String Product { get; init; }

    /// <summary>
    /// The version of the assembly.
    /// </summary>
    public System.String Version { get; init; }

    /// <summary>
    /// The company name associated with the assembly.
    /// </summary>
    public System.String Company { get; init; }

    /// <summary>
    /// The copyright information associated with the assembly.
    /// </summary>
    public System.String Copyright { get; init; }

    /// <summary>
    /// The build time of the assembly.
    /// </summary>
    public System.DateTime BuildTime { get; init; }

    /// <summary>
    /// The file version of the assembly.
    /// </summary>
    public System.String FileVersion { get; init; }

    /// <summary>
    /// The name of the assembly.
    /// </summary>
    public System.String AssemblyName { get; init; }

    /// <summary>
    /// The informational version of the assembly.
    /// </summary>
    public System.String InformationalVersion { get; init; }
}