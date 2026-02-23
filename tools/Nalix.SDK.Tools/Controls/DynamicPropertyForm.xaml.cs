// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Configuration;
using Nalix.SDK.Tools.Models;
using Nalix.SDK.Tools.Services;
using Nalix.SDK.Tools.ViewModels;

namespace Nalix.SDK.Tools.Controls;

/// <summary>
/// Displays a reflection-driven packet form using controls created by <see cref="PropertyEditorFactory"/>.
/// </summary>
public partial class DynamicPropertyForm : UserControl
{
    private static readonly IPropertyEditorFactory PropertyEditorFactoryInstance = new PropertyEditorFactory();
    private INotifyCollectionChanged? _currentCollection;

    /// <summary>
    /// Identifies the <see cref="ItemsSource"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<PropertyNodeViewModel>),
            typeof(DynamicPropertyForm),
            new PropertyMetadata(null, OnItemsSourceChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicPropertyForm"/> class.
    /// </summary>
    public DynamicPropertyForm() => this.InitializeComponent();

    /// <summary>
    /// Gets or sets the property nodes rendered by the form.
    /// </summary>
    public IEnumerable<PropertyNodeViewModel>? ItemsSource
    {
        get => (IEnumerable<PropertyNodeViewModel>?)this.GetValue(ItemsSourceProperty);
        set => this.SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        => ((DynamicPropertyForm)dependencyObject).HandleItemsSourceChanged(
            e.OldValue as IEnumerable<PropertyNodeViewModel>,
            e.NewValue as IEnumerable<PropertyNodeViewModel>);

    private void HandleItemsSourceChanged(
        IEnumerable<PropertyNodeViewModel>? previousItems,
        IEnumerable<PropertyNodeViewModel>? nextItems)
    {
        if (_currentCollection is not null)
        {
            _currentCollection.CollectionChanged -= this.HandleCollectionChanged;
            _currentCollection = null;
        }

        if (nextItems is INotifyCollectionChanged notifyCollectionChanged)
        {
            _currentCollection = notifyCollectionChanged;
            _currentCollection.CollectionChanged += this.HandleCollectionChanged;
        }

        this.Rebuild();
    }

    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => this.Rebuild();

    private void Rebuild()
    {
        HostPanel.Children.Clear();

        if (this.ItemsSource is null)
        {
            return;
        }

        bool hasAny = false;
        foreach (PropertyNodeViewModel propertyNode in this.ItemsSource)
        {
            hasAny = true;
            _ = HostPanel.Children.Add(this.CreatePropertyElement(propertyNode));
        }

        if (!hasAny)
        {
            PacketToolTextConfig texts = ToolResourceHelper.GetTexts();
            _ = HostPanel.Children.Add(new TextBlock
            {
                Text = texts.DynamicFormEmptyMessage,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
    }

    private FrameworkElement CreatePropertyElement(PropertyNodeViewModel propertyNode)
    {
        if (propertyNode.EditorKind == EditorKind.Complex)
        {
            return PropertyEditorFactoryInstance.CreateEditor(propertyNode);
        }

        Grid grid = new()
        {
            Margin = new Thickness(0, 0, 0, 10)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        StackPanel labelPanel = new();
        _ = labelPanel.Children.Add(new TextBlock
        {
            Text = propertyNode.DisplayName,
            FontWeight = FontWeights.SemiBold
        });
        _ = labelPanel.Children.Add(new TextBlock
        {
            Text = propertyNode.TypeDisplayName,
            Foreground = Brushes.DimGray,
            FontSize = 11
        });
        Grid.SetColumn(labelPanel, 0);
        _ = grid.Children.Add(labelPanel);

        StackPanel editorPanel = new();
        _ = editorPanel.Children.Add(PropertyEditorFactoryInstance.CreateEditor(propertyNode));

        TextBlock errorBlock = new()
        {
            Foreground = Brushes.IndianRed,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _ = errorBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(PropertyNodeViewModel.ErrorText)) { Source = propertyNode });
        _ = errorBlock.SetBinding(
            VisibilityProperty,
            new Binding(nameof(PropertyNodeViewModel.HasError))
            {
                Source = propertyNode,
                Converter = (IValueConverter)Application.Current.Resources["BooleanToVisibilityConverter"]
            });

        _ = editorPanel.Children.Add(errorBlock);
        Grid.SetColumn(editorPanel, 1);
        _ = grid.Children.Add(editorPanel);

        return grid;
    }
}
