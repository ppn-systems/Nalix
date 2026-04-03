using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Extensions;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Handles packet builder state and commands.
/// </summary>
public sealed class PacketBuilderViewModel : ViewModelBase
{
    private readonly IPacketCatalogService _catalogService;
    private readonly ITcpClientService _tcpClientService;
    private readonly Action<string, string> _showHexViewer;
    private FrameBase? _currentPacket;
    private PacketTypeDescriptor? _selectedPacketType;
    private string _host = "127.0.0.1";
    private string _portText = "57206";
    private string _resolutionText = "Select a packet type to begin editing.";
    private string _currentPacketTitle = "No packet loaded";
    private string _currentPacketSummary = "Select a packet type in Packet Builder to begin editing.";
    private bool _isConnected;
    private bool _currentPacketIsReadOnly;
    private bool _suppressAutoLoad;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketBuilderViewModel"/> class.
    /// </summary>
    /// <param name="catalogService">The packet catalog service.</param>
    /// <param name="tcpClientService">The TCP client service.</param>
    /// <param name="showHexViewer">The callback used to open the shared hex viewer.</param>
    public PacketBuilderViewModel(IPacketCatalogService catalogService, ITcpClientService tcpClientService, Action<string, string> showHexViewer)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _tcpClientService = tcpClientService ?? throw new ArgumentNullException(nameof(tcpClientService));
        _showHexViewer = showHexViewer ?? throw new ArgumentNullException(nameof(showHexViewer));

        foreach (PacketTypeDescriptor descriptor in _catalogService.Catalog.PacketTypes)
        {
            this.PacketTypes.Add(descriptor);
        }

        this.ConnectCommand = new AsyncRelayCommand(this.ConnectAsync, this.CanConnect);
        this.DisconnectCommand = new AsyncRelayCommand(this.DisconnectAsync, this.CanDisconnect);
        this.ResetPacketCommand = new RelayCommand(this.ResetPacket, this.CanResetPacket);
        this.SerializePacketCommand = new RelayCommand(this.SerializePacket, this.CanSerializePacket);
        this.SendPacketCommand = new AsyncRelayCommand(this.SendPacketAsync, this.CanSendPacket);

        _tcpClientService.StatusChanged += this.HandleStatusChanged;

        if (this.PacketTypes.Count > 0)
        {
            this.SelectedPacketType = this.PacketTypes[0];
        }
    }

    /// <summary>
    /// Raised when the builder wants to publish a status message.
    /// </summary>
    public event Action<string>? StatusRequested;

    /// <summary>
    /// Gets the discovered packet types available to the builder.
    /// </summary>
    public ObservableCollection<PacketTypeDescriptor> PacketTypes { get; } = [];

    /// <summary>
    /// Gets the reflected property nodes for the current packet.
    /// </summary>
    public ObservableCollection<PropertyNodeViewModel> CurrentProperties { get; } = [];

    /// <summary>
    /// Gets the connect command.
    /// </summary>
    public AsyncRelayCommand ConnectCommand { get; }

    /// <summary>
    /// Gets the disconnect command.
    /// </summary>
    public AsyncRelayCommand DisconnectCommand { get; }

    /// <summary>
    /// Gets the reset command.
    /// </summary>
    public RelayCommand ResetPacketCommand { get; }

    /// <summary>
    /// Gets the serialize command.
    /// </summary>
    public RelayCommand SerializePacketCommand { get; }

    /// <summary>
    /// Gets the send command.
    /// </summary>
    public AsyncRelayCommand SendPacketCommand { get; }

    /// <summary>
    /// Gets or sets the connection host.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the connection port.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the selected packet type.
    /// </summary>
    public PacketTypeDescriptor? SelectedPacketType
    {
        get => _selectedPacketType;
        set
        {
            if (!this.SetProperty(ref _selectedPacketType, value))
            {
                return;
            }

            if (!_suppressAutoLoad && value is not null)
            {
                this.LoadDescriptor(value, false);
            }

            this.NotifyCommandStates();
        }
    }

    /// <summary>
    /// Gets the builder note text.
    /// </summary>
    public string ResolutionText
    {
        get => _resolutionText;
        private set => this.SetProperty(ref _resolutionText, value);
    }

    /// <summary>
    /// Gets the builder panel title.
    /// </summary>
    public string CurrentPacketTitle
    {
        get => _currentPacketTitle;
        private set => this.SetProperty(ref _currentPacketTitle, value);
    }

    /// <summary>
    /// Gets the builder panel summary.
    /// </summary>
    public string CurrentPacketSummary
    {
        get => _currentPacketSummary;
        private set => this.SetProperty(ref _currentPacketSummary, value);
    }

    /// <summary>
    /// Gets a value indicating whether the TCP client is connected.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether the current packet is read-only.
    /// </summary>
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

    /// <summary>
    /// Loads a packet snapshot into the builder.
    /// </summary>
    /// <param name="snapshot">The packet snapshot to open.</param>
    /// <param name="isReadOnly">Whether the loaded packet should be read-only.</param>
    /// <returns><see langword="true"/> when the snapshot was loaded successfully.</returns>
    public bool TryOpenSnapshot(PacketSnapshot snapshot, bool isReadOnly)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        FrameBase frame = _catalogService.Deserialize(snapshot.RawBytes);
        PacketTypeDescriptor? descriptor = _catalogService.FindByType(frame.GetType());
        if (descriptor is null)
        {
            return false;
        }

        this.ShowPacket(frame, descriptor, isReadOnly);
        this.ResolutionText = isReadOnly
            ? $"Loaded received packet snapshot for {descriptor.FullName} in read-only mode."
            : $"Reopened sent packet snapshot for {descriptor.FullName}.";
        return true;
    }

    /// <summary>
    /// Shows a specific packet instance in the builder.
    /// </summary>
    /// <param name="packet">The packet instance.</param>
    /// <param name="descriptor">The packet descriptor.</param>
    /// <param name="isReadOnly">Whether the packet should be read-only.</param>
    public void ShowPacket(FrameBase packet, PacketTypeDescriptor descriptor, bool isReadOnly)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(descriptor);

        _suppressAutoLoad = true;
        try
        {
            this.SelectedPacketType = descriptor;
        }
        finally
        {
            _suppressAutoLoad = false;
        }

        _currentPacket = packet;
        this.CurrentPacketIsReadOnly = isReadOnly;
        this.RebuildPropertyNodes(packet, descriptor, isReadOnly);
        _ = this.TryRefreshSerializedPacket(out _);
    }

    private bool CanConnect() => !this.IsConnected && !string.IsNullOrWhiteSpace(this.Host) && this.TryParsePort(out _);

    private bool CanDisconnect() => this.IsConnected;

    private bool CanResetPacket() => this.SelectedPacketType is not null && !this.CurrentPacketIsReadOnly;

    private bool CanSerializePacket() => _currentPacket is not null;

    private bool CanSendPacket() => _currentPacket is not null && !this.CurrentPacketIsReadOnly && this.IsConnected;

    private async Task ConnectAsync()
    {
        if (!this.TryParsePort(out ushort port))
        {
            this.RaiseStatusRequested("The port must be a valid unsigned 16-bit integer.");
            return;
        }

        try
        {
            await _tcpClientService.ConnectAsync(new ConnectionSettings
            {
                Host = this.Host.Trim(),
                Port = port
            }).ConfigureAwait(true);
            this.IsConnected = _tcpClientService.IsConnected;
        }
        catch (Exception exception)
        {
            this.RaiseStatusRequested(exception.Message);
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _tcpClientService.DisconnectAsync().ConfigureAwait(true);
            this.IsConnected = _tcpClientService.IsConnected;
        }
        catch (Exception exception)
        {
            this.RaiseStatusRequested(exception.Message);
        }
    }

    private void ResetPacket()
    {
        if (this.SelectedPacketType is null)
        {
            return;
        }

        this.LoadDescriptor(this.SelectedPacketType, false);
        this.RaiseStatusRequested("Packet editor reset to a fresh instance.");
    }

    private void SerializePacket()
    {
        if (this.TryRefreshSerializedPacket(out string hex) && _currentPacket is not null)
        {
            this.RaiseStatusRequested($"Serialized {_currentPacket.GetType().Name} into {_currentPacket.Length:N0} bytes.");
            _showHexViewer($"Serialized: {_currentPacket.GetType().Name}", hex);
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
            this.RaiseStatusRequested(exception.Message);
        }
    }

    private void LoadDescriptor(PacketTypeDescriptor descriptor, bool isReadOnly)
    {
        FrameBase packet = _catalogService.CreatePacket(descriptor);
        if (packet.Protocol == ProtocolType.NONE)
        {
            packet.Protocol = ProtocolType.TCP;
        }

        this.ShowPacket(packet, descriptor, isReadOnly);
        this.ResolutionText = $"Loaded {descriptor.FullName} into the packet builder. Packet identity is defined by MagicNumber.";
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
        catch
        {
            this.CurrentPacketSummary = this.BuildPacketSummary(_currentPacket);
            this.RaiseStatusRequested("Serialization failed for the current packet.");
            return false;
        }
    }

    private string BuildPacketSummary(FrameBase frame)
        => $"{frame.GetType().FullName} | Magic 0x{frame.MagicNumber:X8} | OpCode 0x{frame.OpCode:X4} | Length {frame.Length:N0} bytes";

    private bool TryParsePort(out ushort port)
        => ushort.TryParse(this.PortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port);

    private void HandleStatusChanged(object? sender, string status)
    {
        this.IsConnected = _tcpClientService.IsConnected;
        this.NotifyCommandStates();
    }

    private void RaiseStatusRequested(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            this.StatusRequested?.Invoke(message);
        }
    }

    private void NotifyCommandStates()
    {
        this.ConnectCommand.NotifyCanExecuteChanged();
        this.DisconnectCommand.NotifyCanExecuteChanged();
        this.ResetPacketCommand.NotifyCanExecuteChanged();
        this.SerializePacketCommand.NotifyCanExecuteChanged();
        this.SendPacketCommand.NotifyCanExecuteChanged();
    }
}
