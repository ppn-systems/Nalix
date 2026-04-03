using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Nalix.SDK.Tools.Extensions;
using Nalix.SDK.Tools.Services;

namespace Nalix.SDK.Tools.Controls;

/// <summary>
/// Provides a small hex editor for editing packet byte array properties.
/// </summary>
public partial class HexEditorControl : UserControl
{
    private static readonly SolidColorBrush s_defaultBorderBrush = new(Color.FromRgb(0x33, 0x41, 0x55));

    private bool _isUpdating;

    /// <summary>
    /// Identifies the <see cref="Value"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(byte[]),
            typeof(HexEditorControl),
            new FrameworkPropertyMetadata(
                Array.Empty<byte>(),
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="HexEditorControl"/> class.
    /// </summary>
    public HexEditorControl()
    {
        this.InitializeComponent();
        this.SyncFromValue();
    }

    /// <summary>
    /// Gets or sets the edited byte array value.
    /// </summary>
    public byte[] Value
    {
        get => (byte[])(this.GetValue(ValueProperty) ?? Array.Empty<byte>());
        set => this.SetValue(ValueProperty, value ?? Array.Empty<byte>());
    }

    private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((HexEditorControl)dependencyObject).SyncFromValue();

    private void SyncFromValue()
    {
        if (_isUpdating)
        {
            return;
        }

        var texts = ToolResourceHelper.GetTexts();
        _isUpdating = true;
        try
        {
            HexTextBox.Text = (this.Value ?? Array.Empty<byte>()).ToHexString();
            ByteCountTextBlock.Text = string.Format(CultureInfo.CurrentCulture, texts.HexBytesFormat, this.Value?.Length ?? 0);
            StatusTextBlock.Text = texts.HexHint;
            HexTextBox.BorderBrush = s_defaultBorderBrush;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }

        var texts = ToolResourceHelper.GetTexts();
        try
        {
            byte[] parsed = HexExtensions.ParseHex(HexTextBox.Text);
            this.Value = parsed;
            ByteCountTextBlock.Text = string.Format(CultureInfo.CurrentCulture, texts.HexBytesFormat, parsed.Length);
            StatusTextBlock.Text = texts.HexUpdated;
            HexTextBox.BorderBrush = s_defaultBorderBrush;
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            HexTextBox.BorderBrush = Brushes.IndianRed;
        }
    }

    private void HexTextBox_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }

        try
        {
            _isUpdating = true;
            HexTextBox.Text = (this.Value ?? Array.Empty<byte>()).ToHexString();
            HexTextBox.BorderBrush = s_defaultBorderBrush;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var texts = ToolResourceHelper.GetTexts();
        OpenFileDialog dialog = new()
        {
            Filter = texts.HexDialogFilter,
            Title = texts.HexImportDialogTitle
        };

        if (dialog.ShowDialog() == true)
        {
            this.Value = File.ReadAllBytes(dialog.FileName);
            this.SyncFromValue();
            StatusTextBlock.Text = string.Format(CultureInfo.CurrentCulture, texts.HexLoadedFileStatusFormat, this.Value.Length, Path.GetFileName(dialog.FileName));
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var texts = ToolResourceHelper.GetTexts();
        SaveFileDialog dialog = new()
        {
            Filter = texts.HexDialogFilter,
            FileName = texts.HexExportFileName,
            Title = texts.HexExportDialogTitle
        };

        if (dialog.ShowDialog() == true)
        {
            byte[] value = this.Value ?? Array.Empty<byte>();
            File.WriteAllBytes(dialog.FileName, value);
            StatusTextBlock.Text = string.Format(CultureInfo.CurrentCulture, texts.HexSavedFileStatusFormat, value.Length, Path.GetFileName(dialog.FileName));
        }
    }
}
