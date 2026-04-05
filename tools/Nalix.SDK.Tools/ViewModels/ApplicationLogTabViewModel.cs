using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Nalix.SDK.Tools.Configuration;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents the application-wide log tab.
/// </summary>
public sealed class ApplicationLogTabViewModel : ViewModelBase
{
    private const int MaxLogEntries = 1000;
    private readonly PacketToolTextConfig _texts;
    private ApplicationLogEntryViewModel? _selectedEntry;
    private string _selectedTimestampText = string.Empty;
    private string _selectedSourceText = string.Empty;
    private string _selectedMessageText;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationLogTabViewModel"/> class.
    /// </summary>
    /// <param name="texts">The localized text resources.</param>
    public ApplicationLogTabViewModel(PacketToolTextConfig texts)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _selectedMessageText = _texts.PlaceholderNoLogEntries;
        this.ClearCommand = new RelayCommand(this.Clear, this.CanClear);
    }

    /// <summary>
    /// Gets the recorded log entries.
    /// </summary>
    public ObservableCollection<ApplicationLogEntryViewModel> Entries { get; } = [];

    /// <summary>
    /// Gets the clear command.
    /// </summary>
    public RelayCommand ClearCommand { get; }

    /// <summary>
    /// Gets or sets the selected log entry.
    /// </summary>
    public ApplicationLogEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (this.SetProperty(ref _selectedEntry, value))
            {
                this.SelectedTimestampText = value is null ? string.Empty : value.Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
                this.SelectedSourceText = value is null ? string.Empty : value.Source;
                this.SelectedMessageText = value is null ? _texts.PlaceholderNoLogEntries : value.Message;
                this.ClearCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the selected entry timestamp text.
    /// </summary>
    public string SelectedTimestampText
    {
        get => _selectedTimestampText;
        private set => this.SetProperty(ref _selectedTimestampText, value);
    }

    /// <summary>
    /// Gets the selected entry source text.
    /// </summary>
    public string SelectedSourceText
    {
        get => _selectedSourceText;
        private set => this.SetProperty(ref _selectedSourceText, value);
    }

    /// <summary>
    /// Gets the selected entry message text.
    /// </summary>
    public string SelectedMessageText
    {
        get => _selectedMessageText;
        private set => this.SetProperty(ref _selectedMessageText, value);
    }

    /// <summary>
    /// Adds a new log entry.
    /// </summary>
    /// <param name="source">The entry source.</param>
    /// <param name="message">The log message.</param>
    public void Add(string source, string message)
    {
        ApplicationLogEntryViewModel entry = new(DateTimeOffset.Now, source, message, _texts);
        this.Entries.Insert(0, entry);
        while (this.Entries.Count > MaxLogEntries)
        {
            this.Entries.RemoveAt(this.Entries.Count - 1);
        }

        this.SelectedEntry = entry;
        this.ClearCommand.NotifyCanExecuteChanged();
    }

    private bool CanClear() => this.Entries.Count > 0;

    private void Clear()
    {
        this.Entries.Clear();
        this.SelectedEntry = null;
        this.ClearCommand.NotifyCanExecuteChanged();
    }
}
