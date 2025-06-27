using Nalix.Shared.Internal;

namespace Nalix.Shared.Configuration.Metadata;

/// <summary>
/// Stores metadata about a configuration type.
/// </summary>
internal class ConfigurationMetadata
{
    /// <summary>
    /// Gets or sets the configuration type.
    /// </summary>
    public System.Type ConfigurationType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the bindable properties of the configuration type.
    /// </summary>
    public PropertyMetadata[] BindableProperties { get; init; } = [];
}
