using System;
using System.Windows;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Applies application theme dictionaries at runtime.
/// </summary>
public sealed class AppThemeService : IThemeService
{
    private ResourceDictionary? _currentThemeDictionary;

    /// <inheritdoc/>
    public void ApplyTheme(ToolThemeMode themeMode)
    {
        if (Application.Current is null)
        {
            return;
        }

        ResourceDictionary themeDictionary = new()
        {
            Source = new Uri(
                themeMode == ToolThemeMode.Dark
                    ? "Themes/DarkTheme.xaml"
                    : "Themes/LightTheme.xaml",
                UriKind.Relative)
        };

        if (_currentThemeDictionary is not null)
        {
            _ = Application.Current.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
        }

        Application.Current.Resources.MergedDictionaries.Insert(0, themeDictionary);
        _currentThemeDictionary = themeDictionary;
    }
}
