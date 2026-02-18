// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Win32;
using Nalix.SDK.Tools.Abstractions;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Wraps standard WPF file dialogs for MVVM usage.
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    /// <inheritdoc/>
    public string? PickPacketAssembly(string title, string filter)
    {
        OpenFileDialog dialog = new()
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            DefaultExt = ".dll"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
