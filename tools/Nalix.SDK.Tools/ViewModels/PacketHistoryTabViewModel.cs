using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents one packet history tab.
/// </summary>
public sealed class PacketHistoryTabViewModel : ViewModelBase
{
    private readonly bool _openReadOnly;
    private PacketLogEntry? _selectedEntry;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketHistoryTabViewModel"/> class.
    /// </summary>
    /// <param name="listHeader">The list group header.</param>
    /// <param name="openButtonText">The open button text.</param>
    /// <param name="placeholderTitle">The placeholder title.</param>
    /// <param name="placeholderSummary">The placeholder summary.</param>
    /// <param name="openReadOnly">Whether opened packets should be read-only.</param>
    /// <param name="catalogService">The packet catalog service.</param>
    public PacketHistoryTabViewModel(
        string listHeader,
        string openButtonText,
        string placeholderTitle,
        string placeholderSummary,
        bool openReadOnly,
        IPacketCatalogService catalogService)
    {
        ArgumentNullException.ThrowIfNull(catalogService);

        this.ListHeader = listHeader ?? throw new ArgumentNullException(nameof(listHeader));
        this.OpenButtonText = openButtonText ?? throw new ArgumentNullException(nameof(openButtonText));
        this.Detail = new PacketHistoryDetailViewModel(placeholderTitle, placeholderSummary);
        this.OpenSelectedPacketCommand = new RelayCommand(this.OpenSelectedPacket, this.CanOpenSelectedPacket);
        this.CatalogService = catalogService;
        _openReadOnly = openReadOnly;
    }

    /// <summary>
    /// Raised when the selected packet should be opened in the builder.
    /// </summary>
    public event Action<PacketSnapshot, bool>? OpenRequested;

    /// <summary>
    /// Gets the list header text.
    /// </summary>
    public string ListHeader { get; }

    /// <summary>
    /// Gets the open button text.
    /// </summary>
    public string OpenButtonText { get; }

    /// <summary>
    /// Gets the recorded entries.
    /// </summary>
    public ObservableCollection<PacketLogEntry> Entries { get; } = [];

    /// <summary>
    /// Gets the detail view model for the selected entry.
    /// </summary>
    public PacketHistoryDetailViewModel Detail { get; }

    /// <summary>
    /// Gets the command used to open the selected packet.
    /// </summary>
    public RelayCommand OpenSelectedPacketCommand { get; }

    private IPacketCatalogService CatalogService { get; }

    /// <summary>
    /// Gets or sets the selected history entry.
    /// </summary>
    public PacketLogEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (this.SetProperty(ref _selectedEntry, value))
            {
                if (value is null)
                {
                    this.Detail.Reset();
                }
                else
                {
                    this.Detail.Show(value, this.CatalogService);
                }

                this.OpenSelectedPacketCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Adds a new entry to the history.
    /// </summary>
    /// <param name="entry">The entry to add.</param>
    public void AddEntry(PacketLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        this.Entries.Insert(0, entry);
        this.SelectedEntry = entry;
    }

    private bool CanOpenSelectedPacket() => this.SelectedEntry is not null;

    private void OpenSelectedPacket()
    {
        if (this.SelectedEntry is not null)
        {
            this.OpenRequested?.Invoke(this.SelectedEntry.Snapshot, _openReadOnly);
        }
    }
}
