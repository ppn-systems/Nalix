using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Coordinates the main window and cross-tab interactions.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IPacketCatalogService _catalogService;
    private readonly PacketToolTextConfig _texts;
    private readonly IAppConfigurationService _configurationService;
    private readonly IThemeService _themeService;
    private string _statusText;
    private ThemeOption? _selectedThemeOption;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    public MainWindowViewModel(
        IPacketCatalogService catalogService,
        ITcpClientService tcpClientService,
        IAppConfigurationService configurationService,
        IThemeService themeService)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        ArgumentNullException.ThrowIfNull(tcpClientService);
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _texts = _configurationService.Texts;
        _statusText = _texts.StatusReady;

        this.HexViewer = new HexViewerViewModel(_texts);
        this.PacketBuilder = new PacketBuilderViewModel(_catalogService, tcpClientService, _texts, this.ShowHexViewer);
        this.PacketRegistryBrowser = new PacketRegistryBrowserViewModel(this.PacketBuilder.PacketTypes, _texts);
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
        this.PacketBuilder.StatusRequested += this.HandleBuilderStatusRequested;
        tcpClientService.StatusChanged += this.HandleStatusChanged;
        tcpClientService.PacketSent += this.HandlePacketSent;
        tcpClientService.PacketReceived += this.HandlePacketReceived;

        if (this.PacketBuilder.SelectedPacketType is not null)
        {
            this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;
        }

        this.SelectedThemeOption = this.ThemeOptions.FirstOrDefault(option => option.Mode == _configurationService.Appearance.ThemeMode)
            ?? this.ThemeOptions[0];

        this.PacketBuilder.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(PacketBuilderViewModel.SelectedPacketType), StringComparison.Ordinal))
            {
                this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;
            }
        };
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

    private void HandleOpenRequested(PacketSnapshot snapshot, bool isReadOnly)
    {
        try
        {
            if (!this.PacketBuilder.TryOpenSnapshot(snapshot, isReadOnly))
            {
                this.StatusText = string.Format(CultureInfo.CurrentCulture, _texts.StatusPacketTypeUnavailableFormat, snapshot.PacketTypeName);
                return;
            }

            this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;
        }
        catch (Exception exception)
        {
            this.StatusText = string.Format(CultureInfo.CurrentCulture, _texts.StatusUnableOpenSnapshotFormat, exception.Message);
        }
    }

    private void HandleStatusChanged(object? sender, string status) => this.StatusText = status;

    private void HandleBuilderStatusRequested(string status) => this.StatusText = status;

    private void HandlePacketSent(object? sender, PacketLogEntry entry)
    {
        this.SentHistory.AddEntry(entry);
        this.StatusText = string.Format(CultureInfo.CurrentCulture, _texts.StatusSentPacketSuccessFormat, entry.PacketName);
    }

    private void HandlePacketReceived(object? sender, PacketLogEntry entry)
    {
        this.ReceiveHistory.AddEntry(entry);
        this.StatusText = string.Format(CultureInfo.CurrentCulture, _texts.StatusReceivedPacketSuccessFormat, entry.PacketName, entry.DecodeStatus);
    }

    private void ShowHexViewer(string title, string hex) => this.HexViewer.Show(title, hex);
}
