// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Extensions;
using Nalix.SDK.Tools.Models;
using Nalix.SDK.Tools.Services;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Handles packet builder state and commands.
/// </summary>
public sealed class PacketBuilderViewModel : ViewModelBase, IDisposable
{
    private readonly IPacketCatalogService _catalogService;
    private readonly INetworkClientService _tcpClientService;
    private readonly PacketToolTextConfig _texts;
    private readonly Action<string, string> _showHexViewer;
    private IPacket? _currentPacket;
    private PacketTypeDescriptor? _selectedPacketType;
    private string _host = "127.0.0.1";
    private string _portText = "57206";
    private string _resolutionText;
    private string _currentPacketTitle;
    private string _currentPacketSummary;
    private string _repeatCountText = "10";
    private string _repeatDelayText = "250";
    private string _sessionToken = string.Empty;
    private ProtocolType _selectedTransport = ProtocolType.TCP;
    private bool _isConnected;
    private bool _currentPacketIsReadOnly;
    private bool _suppressAutoLoad;
    private CancellationTokenSource? _repeatSendCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketBuilderViewModel"/> class.
    /// </summary>
    /// <param name="catalogService">The packet catalog service.</param>
    /// <param name="tcpClientService">The TCP client service.</param>
    /// <param name="showHexViewer">The callback used to open the shared hex viewer.</param>
    public PacketBuilderViewModel(IPacketCatalogService catalogService, INetworkClientService tcpClientService, PacketToolTextConfig texts, Action<string, string> showHexViewer)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _tcpClientService = tcpClientService ?? throw new ArgumentNullException(nameof(tcpClientService));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _showHexViewer = showHexViewer ?? throw new ArgumentNullException(nameof(showHexViewer));
        _resolutionText = _texts.PlaceholderCurrentPacketSummary;
        _currentPacketTitle = _texts.PlaceholderNoPacketLoaded;
        _currentPacketSummary = _texts.PlaceholderCurrentPacketSummary;

        foreach (PacketTypeDescriptor descriptor in _catalogService.Catalog.PacketTypes)
        {
            this.PacketTypes.Add(descriptor);
        }

        this.ConnectCommand = new AsyncRelayCommand(this.ConnectAsync, this.CanConnect);
        this.DisconnectCommand = new AsyncRelayCommand(this.DisconnectAsync, this.CanDisconnect);
        this.ResetPacketCommand = new RelayCommand(this.ResetPacket, this.CanResetPacket);
        this.SerializePacketCommand = new RelayCommand(this.SerializePacket, this.CanSerializePacket);
        this.SendPacketCommand = new AsyncRelayCommand(this.SendPacketAsync, this.CanSendPacket);
        this.RepeatSendCommand = new AsyncRelayCommand(this.RepeatSendAsync, this.CanRepeatSend);
        this.HandshakeCommand = new AsyncRelayCommand(this.HandshakeAsync, this.CanHandshake);

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
    /// Gets the repeat send command.
    /// </summary>
    public AsyncRelayCommand RepeatSendCommand { get; }

    /// <summary>
    /// Gets the handshake command.
    /// </summary>
    public AsyncRelayCommand HandshakeCommand { get; }

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
    /// Gets the available transport protocols.
    /// </summary>
    public ProtocolType[] Transports { get; } = [ProtocolType.TCP, ProtocolType.UDP];

    /// <summary>
    /// Gets or sets the selected transport protocol.
    /// </summary>
    public ProtocolType SelectedTransport
    {
        get => _selectedTransport;
        set
        {
            if (this.SetProperty(ref _selectedTransport, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    /// <summary>
    /// Gets or sets the UDP session token.
    /// </summary>
    public string SessionToken
    {
        get => _sessionToken;
        set
        {
            if (this.SetProperty(ref _sessionToken, value))
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
    /// Gets or sets the repeat count text.
    /// </summary>
    public string RepeatCountText
    {
        get => _repeatCountText;
        set
        {
            if (this.SetProperty(ref _repeatCountText, value))
            {
                this.NotifyCommandStates();
            }
        }
    }

    /// <summary>
    /// Gets or sets the repeat delay text in milliseconds.
    /// </summary>
    public string RepeatDelayText
    {
        get => _repeatDelayText;
        set
        {
            if (this.SetProperty(ref _repeatDelayText, value))
            {
                this.NotifyCommandStates();
            }
        }
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

        IPacket packet = _catalogService.Deserialize(snapshot.RawBytes);
        PacketTypeDescriptor? descriptor = _catalogService.FindByType(packet.GetType());
        if (descriptor is null)
        {
            return false;
        }

        this.ShowPacket(packet, descriptor, isReadOnly);
        this.ResolutionText = string.Format(
            CultureInfo.CurrentCulture,
            isReadOnly ? _texts.StatusLoadedReceivedSnapshotFormat : _texts.StatusReopenedSentSnapshotFormat,
            descriptor.FullName);
        return true;
    }

    /// <summary>
    /// Shows a specific packet instance in the builder.
    /// </summary>
    /// <param name="packet">The packet instance.</param>
    /// <param name="descriptor">The packet descriptor.</param>
    /// <param name="isReadOnly">Whether the packet should be read-only.</param>
    public void ShowPacket(IPacket packet, PacketTypeDescriptor descriptor, bool isReadOnly)
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

    private bool CanRepeatSend() => this.CanSendPacket() && this.TryParseRepeatOptions(out _, out _);

    private bool CanHandshake() => this.IsConnected;

    private async Task ConnectAsync()
    {
        if (!this.TryParsePort(out ushort port))
        {
            this.RaiseStatusRequested(_texts.StatusPortInvalid);
            return;
        }

        try
        {
            await _tcpClientService.ConnectAsync(new ConnectionSettings
            {
                Host = this.Host.Trim(),
                Port = port,
                Transport = this.SelectedTransport,
                SessionToken = this.SessionToken.Trim()
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

    private async Task HandshakeAsync()
    {
        try
        {
            this.RaiseStatusRequested(_texts.StatusHandshakeStarted);
            await _tcpClientService.HandshakeAsync().ConfigureAwait(true);
            
            // Auto-fill SessionToken if it was received (common for UDP)
            if (_tcpClientService is NetworkClientService service && !service.SessionToken.IsEmpty)
            {
                this.SessionToken = service.SessionToken.ToString();
                this.RaiseStatusRequested(_texts.StatusSessionTokenAutoFilled);
            }

            this.RaiseStatusRequested(_texts.StatusHandshakeSuccess);
        }
        catch (Exception exception)
        {
            this.RaiseStatusRequested(string.Format(CultureInfo.CurrentCulture, _texts.StatusHandshakeFailedFormat, exception.Message));
        }
    }

    private void ResetPacket()
    {
        if (this.SelectedPacketType is null)
        {
            return;
        }

        this.LoadDescriptor(this.SelectedPacketType, false);
        this.RaiseStatusRequested(_texts.StatusPacketEditorReset);
    }

    private void SerializePacket()
    {
        if (this.TryRefreshSerializedPacket(out string hex) && _currentPacket is not null)
        {
            this.RaiseStatusRequested(string.Format(CultureInfo.CurrentCulture, _texts.StatusSerializedFormat, _currentPacket.GetType().Name, _currentPacket.Length));
            _showHexViewer(string.Format(CultureInfo.CurrentCulture, _texts.SerializedViewerTitleFormat, _currentPacket.GetType().Name), hex);
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

    private async Task RepeatSendAsync()
    {
        if (_currentPacket is null || !this.TryRefreshSerializedPacket(out _) || !this.TryParseRepeatOptions(out int count, out int delayMs))
        {
            return;
        }

        if (count <= 0)
        {
            this.RaiseStatusRequested(_texts.StatusRepeatSendCancelled);
            return;
        }

        _repeatSendCts?.Cancel();
        _repeatSendCts?.Dispose();
        _repeatSendCts = new CancellationTokenSource();

        CancellationToken token = _repeatSendCts.Token;
        this.RaiseStatusRequested(string.Format(CultureInfo.CurrentCulture, _texts.StatusRepeatSendStartedFormat, count, delayMs));

        int sentCount = 0;
        try
        {
            for (int index = 0; index < count; index++)
            {
                token.ThrowIfCancellationRequested();
                await _tcpClientService.SendPacketAsync(_currentPacket, token).ConfigureAwait(true);
                sentCount++;

                if (delayMs > 0 && index < count - 1)
                {
                    await Task.Delay(delayMs, token).ConfigureAwait(true);
                }
            }

            this.RaiseStatusRequested(string.Format(CultureInfo.CurrentCulture, _texts.StatusRepeatSendFinishedFormat, sentCount));
        }
        catch (OperationCanceledException)
        {
            this.RaiseStatusRequested(_texts.StatusRepeatSendCancelled);
        }
        catch (Exception exception)
        {
            this.RaiseStatusRequested(exception.Message);
        }
    }

    private void LoadDescriptor(PacketTypeDescriptor descriptor, bool isReadOnly)
    {
        IPacket packet = _catalogService.CreatePacket(descriptor);
        if (packet.Protocol == ProtocolType.NONE)
        {
            packet.Protocol = ProtocolType.TCP;
        }

        this.ShowPacket(packet, descriptor, isReadOnly);
        this.ResolutionText = string.Format(CultureInfo.CurrentCulture, _texts.StatusLoadedPacketBuilderFormat, descriptor.FullName);
    }

    public void ReloadPacketCatalog(PacketCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        this.RefreshPacketTypes(catalog.PacketTypes, this.SelectedPacketType);
    }

    private void RefreshPacketTypes(System.Collections.Generic.IReadOnlyList<PacketTypeDescriptor> packetTypes, PacketTypeDescriptor? previousSelection)
    {
        this.PacketTypes.Clear();
        foreach (PacketTypeDescriptor descriptor in packetTypes)
        {
            this.PacketTypes.Add(descriptor);
        }

        PacketTypeDescriptor? selection = previousSelection is null
            ? this.PacketTypes.FirstOrDefault()
            : this.PacketTypes.FirstOrDefault(descriptor => descriptor.PacketType == previousSelection.PacketType)
                ?? this.PacketTypes.FirstOrDefault();

        _suppressAutoLoad = true;
        try
        {
            this.SelectedPacketType = selection;
        }
        finally
        {
            _suppressAutoLoad = false;
        }

        if (selection is null)
        {
            _currentPacket = null;
            this.CurrentPacketIsReadOnly = false;
            this.CurrentProperties.Clear();
            this.CurrentPacketTitle = _texts.PlaceholderNoPacketLoaded;
            this.CurrentPacketSummary = _texts.PlaceholderCurrentPacketSummary;
            this.ResolutionText = _texts.PlaceholderCurrentPacketSummary;
            return;
        }

        if (_currentPacket is not null && selection.PacketType == _currentPacket.GetType())
        {
            PacketTypeDescriptor activeDescriptor = selection;
            this.RebuildPropertyNodes(_currentPacket, activeDescriptor, this.CurrentPacketIsReadOnly);
            _ = this.TryRefreshSerializedPacket(out _);
            return;
        }

        if (this.SelectedPacketType is not null)
        {
            this.LoadDescriptor(this.SelectedPacketType, false);
        }
    }

    private void RebuildPropertyNodes(IPacket packet, PacketTypeDescriptor descriptor, bool isReadOnly)
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

        this.CurrentPacketTitle = isReadOnly
            ? $"{descriptor.Name} ({_texts.ReadOnlySuffix})"
            : descriptor.Name;
    }

    private void HandleCurrentPacketChanged() => _ = this.TryRefreshSerializedPacket(out _);

    private bool TryRefreshSerializedPacket(out string hex)
    {
        hex = string.Empty;
        if (_currentPacket is null)
        {
            this.CurrentPacketSummary = _texts.PlaceholderCurrentPacketSummary;
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
            this.RaiseStatusRequested(_texts.StatusSerializationFailed);
            return false;
        }
    }

    private string BuildPacketSummary(IPacket packet)
        => string.Format(CultureInfo.CurrentCulture, _texts.BuilderSummaryFormat, packet.GetType().FullName, packet.MagicNumber, packet.OpCode, packet.Length);

    private bool TryParsePort(out ushort port)
        => ushort.TryParse(this.PortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port);

    private void HandleStatusChanged(object? sender, string status)
    {
        this.IsConnected = _tcpClientService.IsConnected;
        this.NotifyCommandStates();
    }

    private bool TryParseRepeatOptions(out int count, out int delayMs)
    {
        bool countOk = int.TryParse(this.RepeatCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
        bool delayOk = int.TryParse(this.RepeatDelayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out delayMs);
        return countOk && delayOk && count > 0 && delayMs >= 0;
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
        this.RepeatSendCommand.NotifyCanExecuteChanged();
        this.HandshakeCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _tcpClientService.StatusChanged -= this.HandleStatusChanged;
        _repeatSendCts?.Cancel();
        _repeatSendCts?.Dispose();
        _repeatSendCts = null;
    }
}
