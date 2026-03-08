// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Nalix.Chat.Client.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow() => this.InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnClosed(EventArgs e)
    {
        if (this.DataContext is IAsyncDisposable disposable)
        {
            _ = disposable.DisposeAsync();
        }

        base.OnClosed(e);
    }
}
