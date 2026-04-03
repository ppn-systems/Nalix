using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Nalix.SDK.Tools.Extensions;

namespace Nalix.SDK.Tools.Controls;

/// <summary>
/// Provides a small hex editor for editing packet byte array properties.
/// </summary>
public partial class HexEditorControl : UserControl
{
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

        _isUpdating = true;
        try
        {
            HexTextBox.Text = (this.Value ?? Array.Empty<byte>()).ToHexString();
            ByteCountTextBlock.Text = $"{this.Value?.Length ?? 0:N0} bytes";
            StatusTextBlock.Text = "Hex input accepts spaces and line breaks.";
            HexTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
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

        try
        {
            byte[] parsed = HexExtensions.ParseHex(HexTextBox.Text);
            this.Value = parsed;
            ByteCountTextBlock.Text = $"{parsed.Length:N0} bytes";
            StatusTextBlock.Text = "Hex value updated.";
            HexTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
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
            HexTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
            Title = "Import binary payload"
        };

        if (dialog.ShowDialog() == true)
        {
            this.Value = File.ReadAllBytes(dialog.FileName);
            this.SyncFromValue();
            StatusTextBlock.Text = $"Loaded {this.Value.Length:N0} bytes from {Path.GetFileName(dialog.FileName)}.";
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
            FileName = "payload.bin",
            Title = "Export binary payload"
        };

        if (dialog.ShowDialog() == true)
        {
            byte[] value = this.Value ?? Array.Empty<byte>();
            File.WriteAllBytes(dialog.FileName, value);
            StatusTextBlock.Text = $"Saved {value.Length:N0} bytes to {Path.GetFileName(dialog.FileName)}.";
        }
    }
}
