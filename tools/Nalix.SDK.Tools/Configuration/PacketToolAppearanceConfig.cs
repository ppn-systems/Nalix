// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Configuration.Binding;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Configuration;

/// <summary>
/// Stores appearance options for the Nalix SDK tools application.
/// </summary>
public sealed class PacketToolAppearanceConfig : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the preferred application theme.
    /// </summary>
    public ToolThemeMode ThemeMode { get; set; } = ToolThemeMode.Light;
}
