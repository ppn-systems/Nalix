using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Models;
using System.Linq;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents one packet history tab.
/// </summary>
public sealed class PacketHistoryTabViewModel : ViewModelBase
{
    private const int MaxHistoryEntries = 500;
    private readonly bool _openReadOnly;
    private readonly PacketToolTextConfig _texts;
    private PacketLogEntry? _selectedEntry;
    private string _diffSummary;
    private string _diffText;

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
        IPacketCatalogService catalogService,
        PacketToolTextConfig texts)
    {
        ArgumentNullException.ThrowIfNull(catalogService);
        ArgumentNullException.ThrowIfNull(texts);

        this.ListHeader = listHeader ?? throw new ArgumentNullException(nameof(listHeader));
        this.OpenButtonText = openButtonText ?? throw new ArgumentNullException(nameof(openButtonText));
        _texts = texts;
        this.Detail = new PacketHistoryDetailViewModel(texts, placeholderTitle, placeholderSummary);
        this.OpenSelectedPacketCommand = new RelayCommand(this.OpenSelectedPacket, this.CanOpenSelectedPacket);
        this.ComparePreviousCommand = new RelayCommand(this.ComparePrevious, this.CanComparePrevious);
        this.CatalogService = catalogService;
        _openReadOnly = openReadOnly;
        _diffSummary = texts.PlaceholderNoPreviousPacketForDiff;
        _diffText = string.Empty;
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

    /// <summary>
    /// Gets the command used to compare the selected packet with the previous one.
    /// </summary>
    public RelayCommand ComparePreviousCommand { get; }

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
                    this.DiffSummary = _texts.PlaceholderNoPreviousPacketForDiff;
                    this.DiffText = string.Empty;
                }
                else
                {
                    this.Detail.Show(value, this.CatalogService);
                    this.UpdateDiff();
                }

                this.OpenSelectedPacketCommand.NotifyCanExecuteChanged();
                this.ComparePreviousCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the diff summary.
    /// </summary>
    public string DiffSummary
    {
        get => _diffSummary;
        private set => this.SetProperty(ref _diffSummary, value);
    }

    /// <summary>
    /// Gets the diff text.
    /// </summary>
    public string DiffText
    {
        get => _diffText;
        private set => this.SetProperty(ref _diffText, value);
    }

    /// <summary>
    /// Adds a new entry to the history.
    /// </summary>
    /// <param name="entry">The entry to add.</param>
    public void AddEntry(PacketLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        this.Entries.Insert(0, entry);
        while (this.Entries.Count > MaxHistoryEntries)
        {
            this.Entries.RemoveAt(this.Entries.Count - 1);
        }

        this.SelectedEntry = entry;
    }

    private bool CanOpenSelectedPacket() => this.SelectedEntry is not null;

    private bool CanComparePrevious() => this.GetPreviousEntry(this.SelectedEntry) is not null;

    private void OpenSelectedPacket()
    {
        if (this.SelectedEntry is not null)
        {
            this.OpenRequested?.Invoke(this.SelectedEntry.Snapshot, _openReadOnly);
        }
    }

    private void ComparePrevious()
    {
        if (this.SelectedEntry is not null)
        {
            this.UpdateDiff();
        }
    }

    private void UpdateDiff()
    {
        if (this.SelectedEntry is null)
        {
            this.DiffSummary = _texts.PlaceholderNoPreviousPacketForDiff;
            this.DiffText = string.Empty;
            return;
        }

        PacketLogEntry? previous = this.GetPreviousEntry(this.SelectedEntry);
        if (previous is null)
        {
            this.DiffSummary = _texts.PlaceholderNoPreviousPacketForDiff;
            this.DiffText = string.Empty;
            return;
        }

        byte[] left = this.SelectedEntry.Snapshot.RawBytes;
        byte[] right = previous.Snapshot.RawBytes;
        int maxLength = Math.Max(left.Length, right.Length);
        int diffCount = 0;
        System.Text.StringBuilder builder = new(maxLength * 18);

        _ = builder.AppendLine(string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _texts.PacketDiffLengthLineFormat,
            left.Length,
            right.Length));

        for (int index = 0; index < maxLength; index++)
        {
            byte? leftByte = index < left.Length ? left[index] : null;
            byte? rightByte = index < right.Length ? right[index] : null;

            if (leftByte == rightByte)
            {
                continue;
            }

            diffCount++;
            if (diffCount <= 64)
            {
                string leftText = leftByte.HasValue ? leftByte.Value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) : "--";
                string rightText = rightByte.HasValue ? rightByte.Value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) : "--";
                _ = builder.AppendLine(string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _texts.PacketDiffLineFormat,
                    index,
                    leftText,
                    rightText));
            }
        }

        this.DiffSummary = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _texts.PacketDiffSummaryFormat,
            this.SelectedEntry.PacketName,
            previous.PacketName,
            diffCount);
        this.DiffText = diffCount == 0 ? _texts.PacketDiffNoDifferences : builder.ToString();
    }

    private PacketLogEntry? GetPreviousEntry(PacketLogEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }

        int index = this.Entries.IndexOf(entry);
        return index >= 0 && index + 1 < this.Entries.Count ? this.Entries[index + 1] : null;
    }
}
