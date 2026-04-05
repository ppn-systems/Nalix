using Nalix.Common.Environment;
using Nalix.Framework.Configuration;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Configuration;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Loads and persists the tools application configuration from a dedicated INI file.
/// </summary>
public sealed class AppConfigurationService : IAppConfigurationService
{
    private readonly ConfigurationManager _configurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppConfigurationService"/> class.
    /// </summary>
    public AppConfigurationService()
    {
        _configurationManager = ConfigurationManager.Instance;
        this.ConfigFilePath = Directories.GetConfigFilePath("nalix-sdk-tools.ini");
        _configurationManager.SetConfigFilePath(this.ConfigFilePath);
        this.Texts = _configurationManager.Get<PacketToolTextConfig>();
        this.Appearance = _configurationManager.Get<PacketToolAppearanceConfig>();
    }

    /// <inheritdoc/>
    public PacketToolTextConfig Texts { get; }

    /// <inheritdoc/>
    public PacketToolAppearanceConfig Appearance { get; }

    /// <inheritdoc/>
    public string ConfigFilePath { get; }

    /// <inheritdoc/>
    public void Save() => _configurationManager.Flush();
}
