// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Represents one selectable application theme option.
/// </summary>
public sealed class ThemeOption
{
    /// <summary>
    /// Gets or sets the theme mode.
    /// </summary>
    public required ToolThemeMode Mode { get; init; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public required string DisplayName { get; init; }
}
