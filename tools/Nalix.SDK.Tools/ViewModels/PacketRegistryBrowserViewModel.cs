using System;
using System.Collections.ObjectModel;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents the packet registry browser tab state.
/// </summary>
public sealed class PacketRegistryBrowserViewModel : ViewModelBase
{
    private PacketTypeDescriptor? _selectedPacketType;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRegistryBrowserViewModel"/> class.
    /// </summary>
    /// <param name="packetTypes">The packet types available to browse.</param>
    public PacketRegistryBrowserViewModel(ObservableCollection<PacketTypeDescriptor> packetTypes)
        => this.PacketTypes = packetTypes ?? throw new ArgumentNullException(nameof(packetTypes));

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
        set => this.SetProperty(ref _selectedPacketType, value);
    }
}
