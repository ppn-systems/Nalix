// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Coordinates the main window and cross-tab interactions.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IPacketCatalogService _catalogService;
    private readonly INetworkClientService _tcpClientService;
    private readonly PacketToolTextConfig _texts;
    private readonly IAppConfigurationService _configurationService;
    private readonly IThemeService _themeService;
    private readonly Action<string> _packetBuilderStatusHandler;
    private readonly Action<string> _packetRegistryStatusHandler;
    private readonly EventHandler<string> _tcpStatusHandler;
    private readonly PropertyChangedEventHandler _packetBuilderPropertyChangedHandler;
    private string _statusText;
    private ThemeOption? _selectedThemeOption;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    public MainWindowViewModel(
        IPacketCatalogService catalogService,
        INetworkClientService tcpClientService,
        IAppConfigurationService configurationService,
        IThemeService themeService,
        IFileDialogService fileDialogService)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _tcpClientService = tcpClientService ?? throw new ArgumentNullException(nameof(tcpClientService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        ArgumentNullException.ThrowIfNull(fileDialogService);
        _texts = _configurationService.Texts;
        _statusText = string.Empty;

        this.Log = new ApplicationLogTabViewModel(_texts);
        this.HexViewer = new HexViewerViewModel(_texts);
        this.PacketBuilder = new PacketBuilderViewModel(_catalogService, _tcpClientService, _texts, this.ShowHexViewer);
        this.PacketRegistryBrowser = new PacketRegistryBrowserViewModel(this.PacketBuilder.PacketTypes, _catalogService, fileDialogService, _texts);
        this.SentHistory = new PacketHistoryTabViewModel(
            _texts.GroupSentPackets,
            _texts.ButtonReopenSelectedPacket,
            _texts.PlaceholderSentPacketTitle,
            _texts.PlaceholderSentPacketSummary,
            false,
            _catalogService,
            _texts);
        this.ReceiveHistory = new PacketHistoryTabViewModel(
            _texts.GroupReceivedPackets,
            _texts.ButtonInspectSelectedPacket,
            _texts.PlaceholderReceivedPacketTitle,
            _texts.PlaceholderReceivedPacketSummary,
            true,
            _catalogService,
            _texts);

        this.ThemeOptions.Add(new ThemeOption { Mode = ToolThemeMode.Light, DisplayName = _texts.ThemeLight });
        this.ThemeOptions.Add(new ThemeOption { Mode = ToolThemeMode.Dark, DisplayName = _texts.ThemeDark });

        this.SentHistory.OpenRequested += this.HandleOpenRequested;
        this.ReceiveHistory.OpenRequested += this.HandleOpenRequested;
        _packetBuilderStatusHandler = status => this.UpdateStatus(_texts.LogSourceBuilder, status);
        _packetRegistryStatusHandler = status => this.UpdateStatus(_texts.LogSourceRegistry, status);
        _tcpStatusHandler = (_, status) => this.UpdateStatus(_texts.LogSourceTcp, status);
        _packetBuilderPropertyChangedHandler = this.HandlePacketBuilderPropertyChanged;
        this.PacketBuilder.StatusRequested += _packetBuilderStatusHandler;
        this.PacketRegistryBrowser.StatusRequested += _packetRegistryStatusHandler;
        this.PacketRegistryBrowser.AssemblyLoadFailed += this.HandleRegistryLoadFailed;
        this.PacketRegistryBrowser.CatalogReloaded += this.HandleCatalogReloaded;
        _tcpClientService.StatusChanged += _tcpStatusHandler;
        _tcpClientService.PacketSent += this.HandlePacketSent;
        _tcpClientService.PacketReceived += this.HandlePacketReceived;

        if (this.PacketBuilder.SelectedPacketType is not null)
        {
            this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;
        }

        this.SelectedThemeOption = this.ThemeOptions.FirstOrDefault(option => option.Mode == _configurationService.Appearance.ThemeMode)
            ?? this.ThemeOptions[0];
        this.UpdateStatus(_texts.LogSourceSystem, _texts.StatusReady);

        this.PacketBuilder.PropertyChanged += _packetBuilderPropertyChangedHandler;
    }

    /// <summary>
    /// Gets the global status text shown in the header.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => this.SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the available theme options.
    /// </summary>
    public ObservableCollection<ThemeOption> ThemeOptions { get; } = [];

    /// <summary>
    /// Gets or sets the selected theme option.
    /// </summary>
    public ThemeOption? SelectedThemeOption
    {
        get => _selectedThemeOption;
        set
        {
            if (!this.SetProperty(ref _selectedThemeOption, value) || value is null)
            {
                return;
            }

            _configurationService.Appearance.ThemeMode = value.Mode;
            _themeService.ApplyTheme(value.Mode);
            _configurationService.Save();
        }
    }

    /// <summary>
    /// Gets the packet builder workspace.
    /// </summary>
    public PacketBuilderViewModel PacketBuilder { get; }

    /// <summary>
    /// Gets the sent history workspace.
    /// </summary>
    public PacketHistoryTabViewModel SentHistory { get; }

    /// <summary>
    /// Gets the received history workspace.
    /// </summary>
    public PacketHistoryTabViewModel ReceiveHistory { get; }

    /// <summary>
    /// Gets the packet registry browser workspace.
    /// </summary>
    public PacketRegistryBrowserViewModel PacketRegistryBrowser { get; }

    /// <summary>
    /// Gets the shared hex viewer overlay.
    /// </summary>
    public HexViewerViewModel HexViewer { get; }

    /// <summary>
    /// Gets the application log workspace.
    /// </summary>
    public ApplicationLogTabViewModel Log { get; }

    private void HandleOpenRequested(PacketSnapshot snapshot, bool isReadOnly)
    {
        try
        {
            if (!this.PacketBuilder.TryOpenSnapshot(snapshot, isReadOnly))
            {
                this.UpdateStatus(_texts.LogSourceHistory, string.Format(CultureInfo.CurrentCulture, _texts.StatusPacketTypeUnavailableFormat, snapshot.PacketTypeName));
                return;
            }

            this.Dispatch(() =>
            {
                this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;
                this.UpdateStatus(_texts.LogSourceHistory, this.PacketBuilder.CurrentPacketSummary);
            });
        }
        catch (Exception exception)
        {
            this.UpdateStatus(_texts.LogSourceHistory, string.Format(CultureInfo.CurrentCulture, _texts.StatusUnableOpenSnapshotFormat, exception.Message));
        }
    }

    private void HandlePacketSent(object? sender, PacketLogEntry entry)
    {
        this.Dispatch(() =>
        {
            this.SentHistory.AddEntry(entry);
            this.UpdateStatus(_texts.LogSourceTcp, string.Format(CultureInfo.CurrentCulture, _texts.StatusSentPacketSuccessFormat, entry.PacketName));
        });
    }

    private void HandlePacketReceived(object? sender, PacketLogEntry entry)
    {
        this.Dispatch(() =>
        {
            this.ReceiveHistory.AddEntry(entry);
            this.UpdateStatus(_texts.LogSourceTcp, string.Format(CultureInfo.CurrentCulture, _texts.StatusReceivedPacketSuccessFormat, entry.PacketName, entry.DecodeStatus));
        });
    }

    private void HandleCatalogReloaded(PacketCatalog catalog, int addedCount)
    {
        this.Dispatch(() =>
        {
            this.PacketBuilder.ReloadPacketCatalog(catalog);
            this.PacketRegistryBrowser.ReloadFromPacketTypes(catalog.PacketTypes);
            this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;

            if (addedCount > 0 && this.PacketBuilder.IsConnected)
            {
                this.UpdateStatus(_texts.LogSourceRegistry, _texts.StatusPacketAssemblyReconnectRequired);
            }
        });
    }

    private void HandleRegistryLoadFailed(string statusMessage, string detailMessage)
    {
        this.Dispatch(() =>
        {
            this.StatusText = statusMessage;
            this.Log.Add(_texts.LogSourceRegistry, $"{statusMessage} {detailMessage}");
        });
    }

    private void UpdateStatus(string source, string message)
    {
        this.Dispatch(() =>
        {
            this.StatusText = message;
            this.Log.Add(source, message);
        });
    }

    private void Dispatch(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(action);
            return;
        }

        action();
    }

    private void ShowHexViewer(string title, string hex) => this.Dispatch(() => this.HexViewer.Show(title, hex));

    private void HandlePacketBuilderPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.Equals(args.PropertyName, nameof(PacketBuilderViewModel.SelectedPacketType), StringComparison.Ordinal))
        {
            this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        this.SentHistory.OpenRequested -= this.HandleOpenRequested;
        this.ReceiveHistory.OpenRequested -= this.HandleOpenRequested;
        this.PacketBuilder.StatusRequested -= _packetBuilderStatusHandler;
        this.PacketRegistryBrowser.StatusRequested -= _packetRegistryStatusHandler;
        this.PacketRegistryBrowser.AssemblyLoadFailed -= this.HandleRegistryLoadFailed;
        this.PacketRegistryBrowser.CatalogReloaded -= this.HandleCatalogReloaded;
        _tcpClientService.StatusChanged -= _tcpStatusHandler;
        _tcpClientService.PacketSent -= this.HandlePacketSent;
        _tcpClientService.PacketReceived -= this.HandlePacketReceived;
        this.PacketBuilder.PropertyChanged -= _packetBuilderPropertyChangedHandler;
        this.PacketBuilder.Dispose();
    }
}
