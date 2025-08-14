// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Configuration.Internal;

/// <summary>
/// Stores metadata about a configuration type.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("{ConfigurationType?.Name,nq} ({BindableProperties?.Length ?? 0} props)")]
internal class ConfigurationMetadata
{
    /// <summary>
    /// Gets or sets the configuration type.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.NotNull]
    public System.Type ConfigurationType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the bindable properties of the configuration type.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.NotNull]
    public PropertyMetadata[] BindableProperties { get; init; } = [];
}
