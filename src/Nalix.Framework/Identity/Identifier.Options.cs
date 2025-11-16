using Nalix.Framework.Configuration.Binding;

namespace Nalix.Framework.Identity;

/// <summary>
/// Identifier configuration options.
/// </summary>
public class IdentifierOptions : ConfigurationLoader
{
    /// <summary>
    /// Machine ID (1-1023) used in distributed ID generation.
    /// </summary>
    public System.UInt16 MachineId { get; set; } = 1;
}
