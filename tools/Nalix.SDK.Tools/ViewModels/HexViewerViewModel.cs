// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Extensions;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents the transient hex viewer overlay state.
/// </summary>
public sealed class HexViewerViewModel : ViewModelBase
{
    private readonly PacketToolTextConfig _texts;
    private string _title;
    private string _hex = string.Empty;
    private string _copyText = string.Empty;
    private bool _isVisible;

    /// <summary>
    /// Initializes a new instance of the <see cref="HexViewerViewModel"/> class.
    /// </summary>
    public HexViewerViewModel(PacketToolTextConfig texts)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _title = _texts.HexViewerTitle;
        this.CopyCommand = new RelayCommand(this.Copy, this.CanCopy);
        this.CloseCommand = new RelayCommand(this.Close, this.CanClose);
    }

    /// <summary>
    /// Gets the command that copies the current hex content.
    /// </summary>
    public RelayCommand CopyCommand { get; }

    /// <summary>
    /// Gets the command that closes the viewer.
    /// </summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>
    /// Gets the viewer title.
    /// </summary>
    public string Title
    {
        get => _title;
        private set => this.SetProperty(ref _title, value);
    }

    /// <summary>
    /// Gets the rendered hex content.
    /// </summary>
    public string Hex
    {
        get => _hex;
        private set
        {
            if (this.SetProperty(ref _hex, value))
            {
                this.CopyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the raw hex text copied to the clipboard.
    /// </summary>
    public string CopyText
    {
        get => _copyText;
        private set
        {
            if (this.SetProperty(ref _copyText, value))
            {
                this.CopyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the overlay is visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (this.SetProperty(ref _isVisible, value))
            {
                this.CopyCommand.NotifyCanExecuteChanged();
                this.CloseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Displays the viewer with the specified title and content.
    /// </summary>
    /// <param name="title">The viewer title.</param>
    /// <param name="hex">The hex content.</param>
    public void Show(string title, string hex)
    {
        this.Title = string.IsNullOrWhiteSpace(title) ? _texts.HexViewerTitle : title;
        byte[] rawBytes = HexExtensions.ParseHex(hex);
        this.CopyText = rawBytes.Length == 0 ? string.Empty : rawBytes.ToHexString();
        this.Hex = rawBytes.Length == 0 ? string.Empty : rawBytes.ToHexDump();
        this.IsVisible = !string.IsNullOrWhiteSpace(this.Hex);
    }

    private bool CanCopy() => this.IsVisible && !string.IsNullOrWhiteSpace(this.CopyText);

    private bool CanClose() => this.IsVisible;

    private void Copy() => Clipboard.SetText(this.CopyText);

    private void Close() => this.IsVisible = false;
}
