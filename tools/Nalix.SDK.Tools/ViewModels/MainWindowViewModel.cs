using System;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Coordinates the main window and cross-tab interactions.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IPacketCatalogService _catalogService;
    private readonly ITcpClientService _tcpClientService;
    private string _statusText = "Ready.";

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="catalogService">The packet catalog service.</param>
    /// <param name="tcpClientService">The TCP client service.</param>
    public MainWindowViewModel(IPacketCatalogService catalogService, ITcpClientService tcpClientService)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _tcpClientService = tcpClientService ?? throw new ArgumentNullException(nameof(tcpClientService));

        this.HexViewer = new HexViewerViewModel();
        this.PacketBuilder = new PacketBuilderViewModel(_catalogService, _tcpClientService, this.ShowHexViewer);
        this.PacketRegistryBrowser = new PacketRegistryBrowserViewModel(this.PacketBuilder.PacketTypes);
        this.SentHistory = new PacketHistoryTabViewModel(
            "Sent Packets",
            "Reopen Selected Packet",
            "Select a sent packet",
            "Sent packet details will appear here.",
            false,
            _catalogService);
        this.ReceiveHistory = new PacketHistoryTabViewModel(
            "Received Packets",
            "Inspect Selected Packet",
            "Select a received packet",
            "Received packet details will appear here.",
            true,
            _catalogService);

        this.SentHistory.OpenRequested += this.HandleOpenRequested;
        this.ReceiveHistory.OpenRequested += this.HandleOpenRequested;
        this.PacketBuilder.StatusRequested += this.HandleBuilderStatusRequested;
        _tcpClientService.StatusChanged += this.HandleStatusChanged;
        _tcpClientService.PacketSent += this.HandlePacketSent;
        _tcpClientService.PacketReceived += this.HandlePacketReceived;

        if (this.PacketBuilder.SelectedPacketType is not null)
        {
            this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;
        }

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
                this.StatusText = $"Packet type {snapshot.PacketTypeName} is not available in the registry browser.";
                return;
            }

            this.PacketRegistryBrowser.SelectedPacketType = this.PacketBuilder.SelectedPacketType;
        }
        catch (Exception exception)
        {
            this.StatusText = $"Unable to open packet snapshot: {exception.Message}";
        }
    }

    private void HandleStatusChanged(object? sender, string status) => this.StatusText = status;

    private void HandleBuilderStatusRequested(string status) => this.StatusText = status;

    private void HandlePacketSent(object? sender, PacketLogEntry entry)
    {
        this.SentHistory.AddEntry(entry);
        this.StatusText = $"{entry.PacketName} sent successfully.";
    }

    private void HandlePacketReceived(object? sender, PacketLogEntry entry)
    {
        this.ReceiveHistory.AddEntry(entry);
        this.StatusText = $"{entry.PacketName} received ({entry.DecodeStatus}).";
    }

    private void ShowHexViewer(string title, string hex) => this.HexViewer.Show(title, hex);
}
