using System.Windows;
using Nalix.SDK.Tools.ViewModels;

namespace Nalix.SDK.Tools.Abstractions;

/// <summary>
/// Creates WPF editor controls for reflected packet properties.
/// </summary>
public interface IPropertyEditorFactory
{
    /// <summary>
    /// Creates the editor element for the specified property node.
    /// </summary>
    /// <param name="propertyNode">The property node to edit.</param>
    /// <returns>The created WPF editor element.</returns>
    FrameworkElement CreateEditor(PropertyNodeViewModel propertyNode);
}
