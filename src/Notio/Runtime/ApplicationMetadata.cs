using System;
using System.Reflection;

namespace Notio.Runtime;

/// <summary>
/// Provides utility methods to retrieve metadata about the current application.
/// </summary>
public static class ApplicationMetadata
{
    private static readonly Lazy<string> CompanyNameLazy = new(() =>
    {
        var attribute = ApplicationInfo.EntryAssembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        return attribute?.Company ?? string.Empty;
    });

    private static readonly Lazy<string> ProductNameLazy = new(() =>
    {
        var attribute = ApplicationInfo.EntryAssembly.GetCustomAttribute<AssemblyProductAttribute>();
        return attribute?.Product ?? string.Empty;
    });

    private static readonly Lazy<string> ProductTrademarkLazy = new(() =>
    {
        var attribute = ApplicationInfo.EntryAssembly.GetCustomAttribute<AssemblyTrademarkAttribute>();
        return attribute?.Trademark ?? string.Empty;
    });

    /// <summary>
    /// Gets the company name.
    /// </summary>
    public static string CompanyName => CompanyNameLazy.Value;

    /// <summary>
    /// Gets the product name.
    /// </summary>
    public static string ProductName => ProductNameLazy.Value;

    /// <summary>
    /// Gets the product trademark.
    /// </summary>
    public static string ProductTrademark => ProductTrademarkLazy.Value;
}
