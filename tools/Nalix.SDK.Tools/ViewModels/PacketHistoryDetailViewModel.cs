using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Extensions;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents one history detail pane for sent or received packets.
/// </summary>
public sealed class PacketHistoryDetailViewModel : ViewModelBase
{
    private readonly PacketToolTextConfig _texts;
    private readonly string _placeholderTitle;
    private readonly string _placeholderSummary;
    private string _title;
    private string _summary;
    private string _lengthText = string.Empty;
    private string _rawHex = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketHistoryDetailViewModel"/> class.
    /// </summary>
    /// <param name="placeholderTitle">The placeholder title.</param>
    /// <param name="placeholderSummary">The placeholder summary.</param>
    public PacketHistoryDetailViewModel(PacketToolTextConfig texts, string placeholderTitle, string placeholderSummary)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _placeholderTitle = placeholderTitle ?? throw new ArgumentNullException(nameof(placeholderTitle));
        _placeholderSummary = placeholderSummary ?? throw new ArgumentNullException(nameof(placeholderSummary));
        _title = placeholderTitle;
        _summary = placeholderSummary;
        this.CopyHexCommand = new RelayCommand(this.CopyHex, this.CanCopyHex);
    }

    /// <summary>
    /// Gets the reflected packet properties shown in the detail pane.
    /// </summary>
    public ObservableCollection<PropertyNodeViewModel> Properties { get; } = [];

    /// <summary>
    /// Gets the command that copies the current raw hex to the clipboard.
    /// </summary>
    public RelayCommand CopyHexCommand { get; }

    /// <summary>
    /// Gets the detail title.
    /// </summary>
    public string Title
    {
        get => _title;
        private set => this.SetProperty(ref _title, value);
    }

    /// <summary>
    /// Gets the detail summary.
    /// </summary>
    public string Summary
    {
        get => _summary;
        private set => this.SetProperty(ref _summary, value);
    }

    /// <summary>
    /// Gets the packet length text.
    /// </summary>
    public string LengthText
    {
        get => _lengthText;
        private set => this.SetProperty(ref _lengthText, value);
    }

    /// <summary>
    /// Gets the raw packet hex.
    /// </summary>
    public string RawHex
    {
        get => _rawHex;
        private set
        {
            if (this.SetProperty(ref _rawHex, value))
            {
                this.CopyHexCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Restores the placeholder state.
    /// </summary>
    public void Reset()
    {
        this.Properties.Clear();
        this.Title = _placeholderTitle;
        this.Summary = _placeholderSummary;
        this.LengthText = string.Empty;
        this.RawHex = string.Empty;
    }

    /// <summary>
    /// Loads packet detail information into the pane.
    /// </summary>
    /// <param name="entry">The packet log entry to display.</param>
    /// <param name="catalogService">The packet catalog service.</param>
    public void Show(PacketLogEntry entry, IPacketCatalogService catalogService)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(catalogService);

        this.Properties.Clear();
        this.Title = entry.PacketName;
        this.LengthText = string.Format(CultureInfo.CurrentCulture, _texts.HexBytesFormat, entry.Snapshot.RawBytes.Length);
        this.RawHex = entry.Snapshot.RawBytes.ToHexString();

        try
        {
            IPacket packet = catalogService.Deserialize(entry.Snapshot.RawBytes);
            PacketTypeDescriptor? descriptor = catalogService.FindByType(packet.GetType());
            this.Summary = descriptor is null
                ? this.BuildFallbackSummary(packet.GetType().FullName ?? packet.GetType().Name, packet.OpCode, packet.MagicNumber, entry.Snapshot.RawBytes.Length, entry.DecodeStatus)
                : this.BuildFallbackSummary(descriptor.FullName, packet.OpCode, packet.MagicNumber, entry.Snapshot.RawBytes.Length, entry.DecodeStatus);

            if (descriptor is null)
            {
                return;
            }

            foreach (PropertyNodeViewModel node in PropertyNodeViewModel.CreateNodes(
                         packet,
                         descriptor.Properties,
                         true,
                         false,
                         static () => { }))
            {
                this.Properties.Add(node);
            }
        }
        catch
        {
            this.Summary = this.BuildFallbackSummary(
                entry.Snapshot.PacketTypeName,
                entry.Snapshot.OpCode,
                entry.Snapshot.MagicNumber,
                entry.Snapshot.RawBytes.Length,
                entry.DecodeStatus);
        }
    }

    private bool CanCopyHex() => !string.IsNullOrWhiteSpace(this.RawHex);

    private void CopyHex() => Clipboard.SetText(this.RawHex);

    private string BuildFallbackSummary(string packetTypeName, ushort opCode, uint magicNumber, int length, string decodeStatus)
        => string.Format(CultureInfo.CurrentCulture, _texts.DetailSummaryFormat, packetTypeName, opCode, magicNumber, length, decodeStatus);
}
