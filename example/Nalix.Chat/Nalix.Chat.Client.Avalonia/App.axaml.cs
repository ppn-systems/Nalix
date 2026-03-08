// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Nalix.Chat.Client.Avalonia.ViewModels;
using Nalix.Chat.Client.Avalonia.Views;
using Nalix.Chat.Client.Core.Networking;
using Nalix.Chat.Client.Core.Services;
using Nalix.Chat.Client.Core.State;
using Nalix.SDK.Options;

namespace Nalix.Chat.Client.Avalonia;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ServiceCollection services = new();

        _ = services.AddSingleton(new TransportOptions
        {
            Address = "127.0.0.1",
            Port = 57216,
            ReconnectEnabled = true,
            EncryptionEnabled = false,
            ResumeEnabled = true,
            ResumeFallbackToHandshake = true
        });

        _ = services.AddSingleton<INetworkClient, NetworkClient>();
        _ = services.AddSingleton<ChatStateStore>();
        _ = services.AddSingleton<DiagnosticsService>();
        _ = services.AddSingleton<DiagnosticsSidebarViewModel>();
        _ = services.AddSingleton<ChatRoomViewModel>();
        _ = services.AddSingleton<MainWindowViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
