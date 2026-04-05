// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Abstractions;

/// <summary>
/// Applies theme resources to the running application.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Applies the specified theme mode to the application resources.
    /// </summary>
    /// <param name="themeMode">The theme mode to apply.</param>
    void ApplyTheme(ToolThemeMode themeMode);
}
