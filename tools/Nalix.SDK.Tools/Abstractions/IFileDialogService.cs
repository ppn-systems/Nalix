// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK.Tools.Abstractions;

/// <summary>
/// Provides file dialog interactions for view models.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Opens a file picker for packet assemblies.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="filter">The dialog filter.</param>
    /// <returns>The selected assembly path, or <see langword="null"/> when cancelled.</returns>
    string? PickPacketAssembly(string title, string filter);
}
