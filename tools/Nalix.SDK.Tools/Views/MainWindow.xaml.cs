// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Windows;

namespace Nalix.SDK.Tools.Views;

/// <summary>
/// Hosts the main packet testing tool shell.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        this.Closed += this.HandleClosed;
    }

    private void HandleClosed(object? sender, EventArgs e)
    {
        if (this.DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
