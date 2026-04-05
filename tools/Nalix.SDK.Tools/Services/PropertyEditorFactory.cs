using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Controls;
using Nalix.SDK.Tools.Models;
using Nalix.SDK.Tools.ViewModels;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Creates WPF editors for packet property nodes by inspecting the inferred editor kind.
/// </summary>
public sealed class PropertyEditorFactory : IPropertyEditorFactory
{
    /// <inheritdoc/>
    public FrameworkElement CreateEditor(PropertyNodeViewModel propertyNode)
    {
        ArgumentNullException.ThrowIfNull(propertyNode);

        return propertyNode.EditorKind switch
        {
            EditorKind.Boolean => this.CreateBooleanEditor(propertyNode),
            EditorKind.Enum => this.CreateEnumEditor(propertyNode),
            EditorKind.ByteArray => this.CreateHexEditor(propertyNode),
            EditorKind.Complex => this.CreateComplexEditor(propertyNode),
            EditorKind.Numeric => this.CreateTextEditor(propertyNode, true),
            EditorKind.Text => this.CreateTextEditor(propertyNode, false),
            EditorKind.Unsupported => throw new NotImplementedException(),
            _ => this.CreateTextEditor(propertyNode, false)
        };
    }

    private FrameworkElement CreateTextEditor(PropertyNodeViewModel propertyNode, bool isNumeric)
    {
        TextBox editor = new()
        {
            MinWidth = 220,
            Padding = new Thickness(8, 6, 8, 6),
            FontFamily = new FontFamily(isNumeric ? "Consolas" : "Segoe UI"),
            IsReadOnly = propertyNode.IsReadOnly,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        _ = editor.SetBinding(
            TextBox.TextProperty,
            new Binding(nameof(PropertyNodeViewModel.TextValue))
            {
                Source = propertyNode,
                Mode = propertyNode.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        return editor;
    }

    private FrameworkElement CreateBooleanEditor(PropertyNodeViewModel propertyNode)
    {
        CheckBox editor = new()
        {
            IsEnabled = !propertyNode.IsReadOnly,
            VerticalAlignment = VerticalAlignment.Center
        };

        _ = editor.SetBinding(
            ToggleButton.IsCheckedProperty,
            new Binding(nameof(PropertyNodeViewModel.BooleanValue))
            {
                Source = propertyNode,
                Mode = propertyNode.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        return editor;
    }

    private FrameworkElement CreateEnumEditor(PropertyNodeViewModel propertyNode)
    {
        ComboBox editor = new()
        {
            MinWidth = 220,
            Padding = new Thickness(4),
            ItemsSource = propertyNode.EnumValues,
            IsEnabled = !propertyNode.IsReadOnly
        };

        _ = editor.SetBinding(
            Selector.SelectedItemProperty,
            new Binding(nameof(PropertyNodeViewModel.SelectedEnumValue))
            {
                Source = propertyNode,
                Mode = propertyNode.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        return editor;
    }

    private FrameworkElement CreateHexEditor(PropertyNodeViewModel propertyNode)
    {
        HexEditorControl editor = new()
        {
            IsEnabled = !propertyNode.IsReadOnly
        };

        _ = editor.SetBinding(
            HexEditorControl.ValueProperty,
            new Binding(nameof(PropertyNodeViewModel.ByteArrayValue))
            {
                Source = propertyNode,
                Mode = propertyNode.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        return editor;
    }

    private FrameworkElement CreateComplexEditor(PropertyNodeViewModel propertyNode)
    {
        return new Expander
        {
            Header = $"{propertyNode.DisplayName} ({propertyNode.TypeDisplayName})",
            IsExpanded = true,
            Margin = new Thickness(0, 0, 0, 10),
            Content = new DynamicPropertyForm
            {
                Margin = new Thickness(0, 12, 0, 0),
                ItemsSource = propertyNode.Children
            }
        };
    }
}
