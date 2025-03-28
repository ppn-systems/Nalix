using Notio.Shared.Internal;
using System;

namespace Notio.Shared.Configuration.Metadata;

/// <summary>
/// Stores metadata about a configuration type.
/// </summary>
internal class ConfigurationMetadata
{
    /// <summary>
    /// Gets or sets the configuration type.
    /// </summary>
    public Type ConfigurationType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the bindable properties of the configuration type.
    /// </summary>
    public PropertyMetadata[] BindableProperties { get; init; } = [];
}
