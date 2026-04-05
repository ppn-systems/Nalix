// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Tools.Configuration;

namespace Nalix.SDK.Tools.Abstractions;

/// <summary>
/// Provides typed UI configuration and localization for the tools application.
/// </summary>
public interface IAppConfigurationService
{
    /// <summary>
    /// Gets the UI text configuration.
    /// </summary>
    PacketToolTextConfig Texts { get; }

    /// <summary>
    /// Gets the appearance configuration.
    /// </summary>
    PacketToolAppearanceConfig Appearance { get; }

    /// <summary>
    /// Gets the active configuration file path.
    /// </summary>
    string ConfigFilePath { get; }

    /// <summary>
    /// Persists the current configuration state to disk.
    /// </summary>
    void Save();
}
