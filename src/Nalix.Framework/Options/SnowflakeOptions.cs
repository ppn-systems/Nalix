using Nalix.Framework.Configuration.Binding;

namespace Nalix.Framework.Options;

/// <summary>
/// Identifier configuration options.
/// </summary>
public class SnowflakeOptions : ConfigurationLoader
{
    /// <summary>
    /// Machine ID (1-1023) used in distributed ID generation.
    /// </summary>
    public System.UInt16 MachineId { get; set; } = 1;
}
