using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents the packet registry browser tab state.
/// </summary>
public sealed class PacketRegistryBrowserViewModel : ViewModelBase
{
    private readonly PacketToolTextConfig _texts;
    private PacketTypeDescriptor? _selectedPacketType;
    private string _selectedPacketDetailSummary = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRegistryBrowserViewModel"/> class.
    /// </summary>
    /// <param name="packetTypes">The packet types available to browse.</param>
    public PacketRegistryBrowserViewModel(ObservableCollection<PacketTypeDescriptor> packetTypes, PacketToolTextConfig texts)
    {
        this.PacketTypes = packetTypes ?? throw new ArgumentNullException(nameof(packetTypes));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
    }

    /// <summary>
    /// Gets the available packet types.
    /// </summary>
    public ObservableCollection<PacketTypeDescriptor> PacketTypes { get; }

    /// <summary>
    /// Gets or sets the selected packet type in the browser.
    /// </summary>
    public PacketTypeDescriptor? SelectedPacketType
    {
        get => _selectedPacketType;
        set
        {
            if (this.SetProperty(ref _selectedPacketType, value))
            {
                this.SelectedPacketDetailSummary = value is null
                    ? string.Empty
                    : string.Format(CultureInfo.CurrentCulture, _texts.RegistryDetailSummaryFormat, value.FullName, value.MagicNumber);
            }
        }
    }

    /// <summary>
    /// Gets the selected packet detail summary.
    /// </summary>
    public string SelectedPacketDetailSummary
    {
        get => _selectedPacketDetailSummary;
        private set => this.SetProperty(ref _selectedPacketDetailSummary, value);
    }
}
