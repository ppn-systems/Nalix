using Nalix.Common.Attributes;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Shared.Tests.Configuration;

/// <summary>
/// Intentionally ends with "Config" so section will be trimmed to "GameSettings".
/// </summary>
public sealed class GameSettingsConfig : ConfigurationLoader
{
    // Supported primitive/struct types
    public System.String Title { get; set; } = "DefaultTitle";
    public System.Int32 Port { get; set; } = 7777;
    public System.Boolean Enabled { get; set; } = false;
    public System.Double Ratio { get; set; } = 1.5;
    public System.DateTime LaunchAt { get; set; } = System.DateTime.SpecifyKind(new System.DateTime(2025, 1, 1), System.DateTimeKind.Utc);
    public DemoLevel Level { get; set; } = DemoLevel.Basic;
    public System.Char Separator { get; set; } = ';';

    [ConfiguredIgnore("should not be populated nor written")]
    public System.String Ignored { get; set; } = "IGNORED";
}
