using System;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents one registry browser row.
/// </summary>
public sealed class PacketRegistryEntryViewModel : ViewModelBase
{
    private bool _isFavorite;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRegistryEntryViewModel"/> class.
    /// </summary>
    /// <param name="descriptor">The packet descriptor.</param>
    /// <param name="isFavorite">Whether the packet starts as a favorite.</param>
    public PacketRegistryEntryViewModel(PacketTypeDescriptor descriptor, bool isFavorite)
    {
        this.Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _isFavorite = isFavorite;
    }

    /// <summary>
    /// Gets the packet descriptor.
    /// </summary>
    public PacketTypeDescriptor Descriptor { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the packet is a favorite.
    /// </summary>
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (this.SetProperty(ref _isFavorite, value))
            {
                this.OnPropertyChanged(nameof(this.DisplayText));
            }
        }
    }

    /// <summary>
    /// Gets the text displayed in the registry list.
    /// </summary>
    public string DisplayText => $"{(this.IsFavorite ? "* " : "  ")}{this.Descriptor.RegistryDisplay}";

    /// <summary>
    /// Gets the text used by the search filter.
    /// </summary>
    public string SearchText => $"{this.Descriptor.Name} {this.Descriptor.FullName} {this.Descriptor.MagicNumber:X8}";
}
