// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents the packet registry browser tab state.
/// </summary>
public sealed class PacketRegistryBrowserViewModel : ViewModelBase
{
    private readonly IPacketCatalogService _catalogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly PacketToolTextConfig _texts;
    private readonly HashSet<string> _favoritePacketNames = new(StringComparer.OrdinalIgnoreCase);
    private PacketTypeDescriptor? _selectedPacketType;
    private PacketRegistryEntryViewModel? _selectedEntry;
    private string _selectedPacketDetailSummary = string.Empty;
    private string _searchText = string.Empty;
    private bool _favoritesOnly;
    private bool _suppressSelectionSync;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRegistryBrowserViewModel"/> class.
    /// </summary>
    /// <param name="packetTypes">The packet types available to browse.</param>
    /// <param name="catalogService">The packet catalog service.</param>
    /// <param name="fileDialogService">The file dialog service.</param>
    /// <param name="texts">The localized text resources.</param>
    public PacketRegistryBrowserViewModel(
        ObservableCollection<PacketTypeDescriptor> packetTypes,
        IPacketCatalogService catalogService,
        IFileDialogService fileDialogService,
        PacketToolTextConfig texts)
    {
        this.PacketTypes = packetTypes ?? throw new ArgumentNullException(nameof(packetTypes));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));

        this.LoadPacketAssemblyCommand = new RelayCommand(this.LoadPacketAssembly);
        this.ToggleFavoriteCommand = new RelayCommand(this.ToggleFavorite, this.CanToggleFavorite);
        this.RebuildEntries();
    }

    /// <summary>
    /// Raised when the packet catalog changes.
    /// </summary>
    public event Action<PacketCatalog, int>? CatalogReloaded;

    /// <summary>
    /// Raised when the browser wants to publish a status message.
    /// </summary>
    public event Action<string>? StatusRequested;

    /// <summary>
    /// Raised when packet assembly loading fails.
    /// </summary>
    public event Action<string, string>? AssemblyLoadFailed;

    /// <summary>
    /// Gets the available packet types.
    /// </summary>
    public ObservableCollection<PacketTypeDescriptor> PacketTypes { get; }

    /// <summary>
    /// Gets the visible registry entries.
    /// </summary>
    public ObservableCollection<PacketRegistryEntryViewModel> Entries { get; } = [];

    /// <summary>
    /// Gets the load packet assembly command.
    /// </summary>
    public RelayCommand LoadPacketAssemblyCommand { get; }

    /// <summary>
    /// Gets the command that toggles the favorite state for the selected packet.
    /// </summary>
    public RelayCommand ToggleFavoriteCommand { get; }

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (this.SetProperty(ref _searchText, value))
            {
                this.RebuildEntries();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether only favorites should be shown.
    /// </summary>
    public bool FavoritesOnly
    {
        get => _favoritesOnly;
        set
        {
            if (this.SetProperty(ref _favoritesOnly, value))
            {
                this.RebuildEntries();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected packet type in the browser.
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

            if (_suppressSelectionSync)
            {
                return;
            }

            this.SelectedEntry = value is null
                ? null
                : this.Entries.FirstOrDefault(entry => entry.Descriptor == value);
        }
    }

    /// <summary>
    /// Gets or sets the selected registry entry.
    /// </summary>
    public PacketRegistryEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!this.SetProperty(ref _selectedEntry, value))
            {
                return;
            }

            _suppressSelectionSync = true;
            try
            {
                this.SelectedPacketType = value?.Descriptor;
            }
            finally
            {
                _suppressSelectionSync = false;
            }

            this.SelectedPacketDetailSummary = value is null
                ? string.Empty
                : string.Format(CultureInfo.CurrentCulture, _texts.RegistryDetailSummaryFormat, value.Descriptor.FullName, value.Descriptor.MagicNumber);
            this.ToggleFavoriteCommand.NotifyCanExecuteChanged();
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

    /// <summary>
    /// Rebuilds the visible entries from the loaded packet descriptors.
    /// </summary>
    public void ReloadFromPacketTypes(IEnumerable<PacketTypeDescriptor> packetTypes)
    {
        ArgumentNullException.ThrowIfNull(packetTypes);

        PacketTypeDescriptor? selected = this.SelectedPacketType;
        this.PacketTypes.Clear();
        foreach (PacketTypeDescriptor descriptor in packetTypes)
        {
            this.PacketTypes.Add(descriptor);
        }

        this.RebuildEntries();
        if (selected is not null)
        {
            this.SelectedPacketType = selected;
        }
    }

    private void LoadPacketAssembly()
    {
        string? assemblyPath = _fileDialogService.PickPacketAssembly(_texts.PacketAssemblyDialogTitle, _texts.PacketAssemblyDialogFilter);
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return;
        }

        int previousCount = this.PacketTypes.Count;

        try
        {
            PacketCatalog catalog = _catalogService.LoadPacketAssembly(assemblyPath);
            int addedCount = Math.Max(0, catalog.PacketTypes.Count - previousCount);
            string fileName = Path.GetFileName(assemblyPath);

            this.StatusRequested?.Invoke(
                addedCount > 0
                    ? string.Format(CultureInfo.CurrentCulture, _texts.StatusPacketAssemblyLoadedFormat, fileName, catalog.PacketTypes.Count)
                    : string.Format(CultureInfo.CurrentCulture, _texts.StatusPacketAssemblyNoNewTypesFormat, fileName));

            this.CatalogReloaded?.Invoke(catalog, addedCount);
        }
        catch (Exception exception)
        {
            this.AssemblyLoadFailed?.Invoke(_texts.StatusPacketAssemblyLoadFailedShort, exception.Message);
        }
    }

    private bool CanToggleFavorite() => this.SelectedEntry is not null;

    private void ToggleFavorite()
    {
        if (this.SelectedEntry is null)
        {
            return;
        }

        this.SelectedEntry.IsFavorite = !this.SelectedEntry.IsFavorite;
        if (this.SelectedEntry.IsFavorite)
        {
            _ = _favoritePacketNames.Add(this.SelectedEntry.Descriptor.FullName);
        }
        else
        {
            _ = _favoritePacketNames.Remove(this.SelectedEntry.Descriptor.FullName);
        }

        this.RebuildEntries();
        this.SelectedEntry = this.Entries.FirstOrDefault(entry => entry.Descriptor == this.SelectedPacketType);
    }

    private void RebuildEntries()
    {
        PacketTypeDescriptor? selectedDescriptor = this.SelectedPacketType;
        string search = this.SearchText.Trim();
        IEnumerable<PacketTypeDescriptor> filtered = this.PacketTypes;

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(descriptor =>
                descriptor.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                descriptor.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                descriptor.MagicNumber.ToString("X8", CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (this.FavoritesOnly)
        {
            filtered = filtered.Where(descriptor => _favoritePacketNames.Contains(descriptor.FullName));
        }

        List<PacketRegistryEntryViewModel> entries =
        [
            .. filtered
                .OrderByDescending(descriptor => _favoritePacketNames.Contains(descriptor.FullName))
                .ThenBy(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
                .Select(descriptor => new PacketRegistryEntryViewModel(descriptor, _favoritePacketNames.Contains(descriptor.FullName)))
        ];

        this.Entries.Clear();
        foreach (PacketRegistryEntryViewModel entry in entries)
        {
            this.Entries.Add(entry);
        }

        this.SelectedEntry = selectedDescriptor is null
            ? this.Entries.FirstOrDefault()
            : this.Entries.FirstOrDefault(entry => entry.Descriptor == selectedDescriptor)
                ?? this.Entries.FirstOrDefault();
    }
}
