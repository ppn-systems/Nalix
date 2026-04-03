using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames;
using Nalix.SDK.Tools.Extensions;
using Nalix.SDK.Tools.Models;
using Nalix.SDK.Tools.Services;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Coordinates the WPF packet testing tool UI.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IPacketCatalogService _catalogService;
    private readonly ITcpClientService _tcpClientService;
    private FrameBase? _currentPacket;
    private PacketTypeDescriptor? _selectedPacketType;
    private PacketLogEntry? _selectedSentHistoryEntry;
    private PacketLogEntry? _selectedReceivedHistoryEntry;
    private string _host = "127.0.0.1";
    private string _portText = "57206";
    private string _statusText = "Ready.";
    private string _resolutionText = "Select a packet type to begin editing.";
    private string _currentPacketTitle = "No packet loaded";
    private string _currentPacketSummary = "Select a packet type in Packet Builder to begin editing.";
    private string _hexViewerTitle = "Hex Viewer";
    private string _hexViewerHex = string.Empty;
    private bool _isHexViewerVisible;
    private bool _isConnected;
    private bool _currentPacketIsReadOnly;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="catalogService">The packet catalog service.</param>
    /// <param name="tcpClientService">The TCP client service.</param>
    public MainWindowViewModel(IPacketCatalogService catalogService, ITcpClientService tcpClientService)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _tcpClientService = tcpClientService ?? throw new ArgumentNullException(nameof(tcpClientService));

        foreach (PacketTypeDescriptor descriptor in _catalogService.Catalog.PacketTypes)
        {
            this.PacketTypes.Add(descriptor);
        }

        this.SentDetail = new PacketHistoryDetailViewModel("Select a sent packet", "Sent packet details will appear here.");
        this.ReceivedDetail = new PacketHistoryDetailViewModel("Select a received packet", "Received packet details will appear here.");

        this.ConnectCommand = new AsyncRelayCommand(this.ConnectAsync, this.CanConnect);
        this.DisconnectCommand = new AsyncRelayCommand(this.DisconnectAsync, this.CanDisconnect);
        this.LoadSelectedPacketTypeCommand = new RelayCommand(this.LoadSelectedPacketType, this.CanLoadSelectedPacketType);
        this.ResetPacketCommand = new RelayCommand(this.ResetPacket, this.CanResetPacket);
        this.SerializePacketCommand = new RelayCommand(this.SerializePacket, this.CanSerializePacket);
        this.SendPacketCommand = new AsyncRelayCommand(this.SendPacketAsync, this.CanSendPacket);
        this.CopyHexViewerCommand = new RelayCommand(this.CopyHexViewer, this.CanCopyHexViewer);
        this.CloseHexViewerCommand = new RelayCommand(this.CloseHexViewer, this.CanCloseHexViewer);
        this.ReopenSelectedSentPacketCommand = new RelayCommand(this.ReopenSelectedSentPacket, this.CanReopenSelectedSentPacket);
        this.InspectSelectedReceivedPacketCommand = new RelayCommand(this.InspectSelectedReceivedPacket, this.CanInspectSelectedReceivedPacket);

        _tcpClientService.StatusChanged += this.HandleStatusChanged;
        _tcpClientService.PacketSent += this.HandlePacketSent;
        _tcpClientService.PacketReceived += this.HandlePacketReceived;

        if (this.PacketTypes.Count > 0)
        {
            this.SelectedPacketType = this.PacketTypes[0];
        }
    }

    public ObservableCollection<PacketTypeDescriptor> PacketTypes { get; } = [];

    public ObservableCollection<PropertyNodeViewModel> CurrentProperties { get; } = [];

    public ObservableCollection<PacketLogEntry> SentHistory { get; } = [];

    public ObservableCollection<PacketLogEntry> ReceivedHistory { get; } = [];

    public PacketHistoryDetailViewModel SentDetail { get; }

    public PacketHistoryDetailViewModel ReceivedDetail { get; }

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand DisconnectCommand { get; }

    public RelayCommand LoadSelectedPacketTypeCommand { get; }

    public RelayCommand ResetPacketCommand { get; }

    public RelayCommand SerializePacketCommand { get; }

    public AsyncRelayCommand SendPacketCommand { get; }

    public RelayCommand CopyHexViewerCommand { get; }

    public RelayCommand CloseHexViewerCommand { get; }

    public RelayCommand ReopenSelectedSentPacketCommand { get; }

    public RelayCommand InspectSelectedReceivedPacketCommand { get; }

    public PacketTypeDescriptor? SelectedPacketType
    {
        get => _selectedPacketType;
        set
        {
            if (this.SetProperty(ref _selectedPacketType, value))
            {
                // Packet identity is determined by explicit packet type selection.
            }

            this.NotifyCommandStates();
        }
    }

    public PacketLogEntry? SelectedSentHistoryEntry
    {
        get => _selectedSentHistoryEntry;
        set
        {
            if (this.SetProperty(ref _selectedSentHistoryEntry, value))
            {
                if (value is null)
                {
                    this.SentDetail.Reset();
                }
                else
                {
                    this.SentDetail.Show(value, _catalogService);
                }

                this.NotifyCommandStates();
            }
        }
    }

    public PacketLogEntry? SelectedReceivedHistoryEntry
    {
        get => _selectedReceivedHistoryEntry;
        set
        {
            if (this.SetProperty(ref _selectedReceivedHistoryEntry, value))
            {
                if (value is null)
                {
                    this.ReceivedDetail.Reset();
                }
                else
                {
                    this.ReceivedDetail.Show(value, _catalogService);
                }

                this.NotifyCommandStates();
            }
        }
    }

    public string Host
    {
        get => _host;
        set
        {
            if (this.SetProperty(ref _host, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    public string PortText
    {
        get => _portText;
        set
        {
            if (this.SetProperty(ref _portText, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.SetProperty(ref _statusText, value);
    }

    public string ResolutionText
    {
        get => _resolutionText;
        private set => this.SetProperty(ref _resolutionText, value);
    }

    public string CurrentPacketTitle
    {
        get => _currentPacketTitle;
        private set => this.SetProperty(ref _currentPacketTitle, value);
    }

    public string CurrentPacketSummary
    {
        get => _currentPacketSummary;
        private set => this.SetProperty(ref _currentPacketSummary, value);
    }

    public string HexViewerTitle
    {
        get => _hexViewerTitle;
        private set => this.SetProperty(ref _hexViewerTitle, value);
    }

    public string HexViewerHex
    {
        get => _hexViewerHex;
        private set
        {
            if (this.SetProperty(ref _hexViewerHex, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    public bool IsHexViewerVisible
    {
        get => _isHexViewerVisible;
        private set
        {
            if (this.SetProperty(ref _isHexViewerVisible, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (this.SetProperty(ref _isConnected, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    public bool CurrentPacketIsReadOnly
    {
        get => _currentPacketIsReadOnly;
        private set
        {
            if (this.SetProperty(ref _currentPacketIsReadOnly, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    private bool CanConnect() => !this.IsConnected && !string.IsNullOrWhiteSpace(this.Host) && this.TryParsePort(out _);

    private bool CanDisconnect() => this.IsConnected;

    private bool CanLoadSelectedPacketType() => this.SelectedPacketType is not null;

    private bool CanResetPacket() => this.SelectedPacketType is not null && !this.CurrentPacketIsReadOnly;

    private bool CanSerializePacket() => _currentPacket is not null;

    private bool CanSendPacket() => _currentPacket is not null && !this.CurrentPacketIsReadOnly && this.IsConnected;

    private bool CanCopyHexViewer() => this.IsHexViewerVisible && !string.IsNullOrWhiteSpace(this.HexViewerHex);

    private bool CanCloseHexViewer() => this.IsHexViewerVisible;

    private bool CanReopenSelectedSentPacket() => this.SelectedSentHistoryEntry is not null;

    private bool CanInspectSelectedReceivedPacket() => this.SelectedReceivedHistoryEntry is not null;

    private async Task ConnectAsync()
    {
        if (!this.TryParsePort(out ushort port))
        {
            this.StatusText = "The port must be a valid unsigned 16-bit integer.";
            return;
        }

        try
        {
            await _tcpClientService.ConnectAsync(new ConnectionSettings
            {
                Host = this.Host.Trim(),
                Port = port
            }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            this.StatusText = exception.Message;
        }
        finally
        {
            this.IsConnected = _tcpClientService.IsConnected;
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _tcpClientService.DisconnectAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            this.StatusText = exception.Message;
        }
        finally
        {
            this.IsConnected = _tcpClientService.IsConnected;
        }
    }

    private void LoadSelectedPacketType()
    {
        if (this.SelectedPacketType is null)
        {
            return;
        }

        FrameBase packet = _catalogService.CreatePacket(this.SelectedPacketType);
        if (packet.Protocol == ProtocolType.NONE)
        {
            packet.Protocol = ProtocolType.TCP;
        }

        this.LoadPacket(packet, this.SelectedPacketType, false);
        this.ResolutionText = $"Loaded {this.SelectedPacketType.FullName} into the packet builder. Packet identity is defined by MagicNumber.";
    }

    private void ResetPacket()
    {
        if (this.SelectedPacketType is null)
        {
            return;
        }

        this.LoadSelectedPacketType();
        this.StatusText = "Packet editor reset to a fresh instance.";
    }

    private void SerializePacket()
    {
        if (this.TryRefreshSerializedPacket(out string hex) && _currentPacket is not null)
        {
            this.StatusText = $"Serialized {_currentPacket.GetType().Name} into {_currentPacket.Length:N0} bytes.";
            this.ShowHexViewer($"Serialized: {_currentPacket.GetType().Name}", hex);
        }
    }

    private async Task SendPacketAsync()
    {
        if (_currentPacket is null || !this.TryRefreshSerializedPacket(out _))
        {
            return;
        }

        try
        {
            await _tcpClientService.SendPacketAsync(_currentPacket).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            this.StatusText = exception.Message;
        }
    }

    private void CopyHexViewer()
    {
        Clipboard.SetText(this.HexViewerHex);
        this.StatusText = "Hex copied to the clipboard.";
    }

    private void CloseHexViewer() => this.IsHexViewerVisible = false;

    private void ReopenSelectedSentPacket()
    {
        if (this.SelectedSentHistoryEntry is not null)
        {
            this.OpenSnapshot(this.SelectedSentHistoryEntry.Snapshot, false);
        }
    }

    private void InspectSelectedReceivedPacket()
    {
        if (this.SelectedReceivedHistoryEntry is not null)
        {
            this.OpenSnapshot(this.SelectedReceivedHistoryEntry.Snapshot, true);
        }
    }

    private void OpenSnapshot(PacketSnapshot snapshot, bool isReadOnly)
    {
        try
        {
            FrameBase frame = _catalogService.Deserialize(snapshot.RawBytes);
            PacketTypeDescriptor? descriptor = _catalogService.FindByType(frame.GetType());
            if (descriptor is null)
            {
                this.StatusText = $"Packet type {frame.GetType().FullName} is not available in the registry browser.";
                return;
            }

            this.SelectedPacketType = descriptor;
            this.LoadPacket(frame, descriptor, isReadOnly);
            this.ResolutionText = isReadOnly
                ? $"Loaded received packet snapshot for {descriptor.FullName} in read-only mode."
                : $"Reopened sent packet snapshot for {descriptor.FullName}.";
        }
        catch (Exception exception)
        {
            this.StatusText = $"Unable to open packet snapshot: {exception.Message}";
        }
    }

    private void LoadPacket(FrameBase packet, PacketTypeDescriptor descriptor, bool isReadOnly)
    {
        _currentPacket = packet;
        this.CurrentPacketIsReadOnly = isReadOnly;
        this.RebuildPropertyNodes(packet, descriptor, isReadOnly);
        _ = this.TryRefreshSerializedPacket(out _);
    }

    private void RebuildPropertyNodes(FrameBase packet, PacketTypeDescriptor descriptor, bool isReadOnly)
    {
        this.CurrentProperties.Clear();
        foreach (PropertyNodeViewModel node in PropertyNodeViewModel.CreateNodes(
                     packet,
                     [.. descriptor.Properties.Where(static definition => !string.Equals(definition.Name, "OpCode", StringComparison.Ordinal))],
                     isReadOnly,
                     !isReadOnly,
                     this.HandleCurrentPacketChanged))
        {
            this.CurrentProperties.Add(node);
        }

        this.CurrentPacketTitle = isReadOnly ? $"{descriptor.Name} (Read-only)" : descriptor.Name;
    }

    private void HandleCurrentPacketChanged() => _ = this.TryRefreshSerializedPacket(out _);

    private bool TryRefreshSerializedPacket(out string hex)
    {
        hex = string.Empty;
        if (_currentPacket is null)
        {
            this.CurrentPacketSummary = "Select a packet type in Packet Builder to begin editing.";
            return false;
        }

        try
        {
            hex = _currentPacket.Serialize().ToHexString();
            this.CurrentPacketSummary = this.BuildPacketSummary(_currentPacket);
            return true;
        }
        catch (Exception exception)
        {
            this.CurrentPacketSummary = this.BuildPacketSummary(_currentPacket);
            this.StatusText = $"Serialization failed: {exception.Message}";
            return false;
        }
    }

    private string BuildPacketSummary(FrameBase frame)
        => $"{frame.GetType().FullName} | Magic 0x{frame.MagicNumber:X8} | OpCode 0x{frame.OpCode:X4} | Length {frame.Length:N0} bytes";

    private bool TryParsePort(out ushort port)
        => ushort.TryParse(this.PortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port);

    private void HandleStatusChanged(object? sender, string status)
    {
        this.StatusText = status;
        this.IsConnected = _tcpClientService.IsConnected;
    }

    private void HandlePacketSent(object? sender, PacketLogEntry entry)
    {
        this.SentHistory.Insert(0, entry);
        this.StatusText = $"{entry.PacketName} sent successfully.";
        this.SelectedSentHistoryEntry = entry;
    }

    private void HandlePacketReceived(object? sender, PacketLogEntry entry)
    {
        this.ReceivedHistory.Insert(0, entry);
        this.StatusText = $"{entry.PacketName} received ({entry.DecodeStatus}).";
        this.SelectedReceivedHistoryEntry = entry;
    }

    private void ShowHexViewer(string title, string hex)
    {
        this.HexViewerTitle = title;
        this.HexViewerHex = hex;
        this.IsHexViewerVisible = !string.IsNullOrWhiteSpace(hex);
    }

    private void NotifyCommandStates()
    {
        this.ConnectCommand.NotifyCanExecuteChanged();
        this.DisconnectCommand.NotifyCanExecuteChanged();
        this.LoadSelectedPacketTypeCommand.NotifyCanExecuteChanged();
        this.ResetPacketCommand.NotifyCanExecuteChanged();
        this.SerializePacketCommand.NotifyCanExecuteChanged();
        this.SendPacketCommand.NotifyCanExecuteChanged();
        this.CopyHexViewerCommand.NotifyCanExecuteChanged();
        this.CloseHexViewerCommand.NotifyCanExecuteChanged();
        this.ReopenSelectedSentPacketCommand.NotifyCanExecuteChanged();
        this.InspectSelectedReceivedPacketCommand.NotifyCanExecuteChanged();
    }
}
