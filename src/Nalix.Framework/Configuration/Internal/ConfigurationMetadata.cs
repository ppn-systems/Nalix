// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Tests.Configuration")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Configuration.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Benchmarks.Configuration")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Configuration.Benchmarks")]
#endif

namespace Nalix.Framework.Configuration.Internal;

/// <summary>
/// Stores metadata about a configuration type.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("{ConfigurationType?.Naming,nq} ({BindableProperties?.Length ?? 0} props)")]
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
