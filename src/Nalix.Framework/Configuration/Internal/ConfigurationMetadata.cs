// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests.")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Configuration.Internal;

/// <summary>
/// Stores metadata about a configuration type.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[DebuggerDisplay("{ConfigurationType?.Naming,nq} ({BindableProperties?.Length ?? 0} props)")]
internal class ConfigurationMetadata
{
    /// <summary>
    /// Gets or sets the optional comment written above the section header.
    /// </summary>
    public string? SectionComment { get; init; }

    /// <summary>
    /// Gets or sets the configuration type.
    /// </summary>
    public Type ConfigurationType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the bindable properties of the configuration type.
    /// </summary>
    public PropertyMetadata[] BindableProperties { get; init; } = [];
}
