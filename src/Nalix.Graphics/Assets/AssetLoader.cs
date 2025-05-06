using Nalix.Logging.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Nalix.Graphics.Assets;

/// <summary>
/// Asset management class. Handles loading/unloading of assets located in a specified root folder.
/// Multiple instances of the AssetManager class can be used to simplify asset memory management.
/// </summary>
public class AssetLoader<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>
    : IDisposable where T : class, IDisposable
{
    /// <summary>
    /// List of supported file endings for this AssetManager
    /// </summary>
    protected String[] _FileEndings;

    /// <summary>
    /// Dictionary containing all loaded assets
    /// </summary>
    protected Dictionary<String, T> _Assets = [];

    // Properties ######################################################################
    /// <summary>
    /// The supported file endings by this <see cref="AssetLoader{T}"/> instance
    /// </summary>
    public IEnumerable<String> FileEndings
    {
        get => _FileEndings;
    }

    /// <summary>
    /// Root-folder to look for Assets
    /// </summary>
    public String RootFolder { get; set; }

    /// <summary>
    /// Determines whether this asset loader should operate in debug mode.
    /// Setting this value to <c>true</c> will cause loading exceptions to be thrown instead of being handled internally.
    /// </summary>
    public Boolean Debug { get; set; }

    /// <summary>
    /// Gets a value indicating whether this AssetManager is disposed.
    /// </summary>
    public bool Disposed { get; private set; }

    // CTOR ############################################################################
    /// <summary>
    /// Creates a new instance of the AssetManager class.
    /// </summary>
    /// <param name="supportedFileEndings">List of File Endings this manager is supposed to support (i.e: .jpg)</param>
    /// <param name="assetRoot">Optional root path of the managed asset folder</param>
    internal AssetLoader(IEnumerable<String> supportedFileEndings, String assetRoot = "")
    {
        RootFolder = assetRoot;
        _FileEndings = [.. new[] { String.Empty }.Concat(supportedFileEndings).Distinct()];
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="AssetLoader{T}"/> class.
    /// </summary>
    ~AssetLoader()
    {
        Dispose(false);
    }

    // Methods #########################################################################
    /// <summary>
    /// Loads or retrieves an already loaded instance of an Asset from a File or Raw Data Source
    /// </summary>
    /// <param name="name">Name of the Resource</param>
    /// <param name="rawData">Optional byte array containing the raw data of the asset</param>
    /// <returns>The managed Asset</returns>
    public virtual T Load(String name, Byte[] rawData = null)
    {
        ObjectDisposedException.ThrowIf(Disposed, nameof(AssetLoader<T>));

        ArgumentNullException.ThrowIfNull(name);
        if (_Assets.TryGetValue(name, out T value)) return value;

        Object param = null;
        try
        {
            param = rawData as Object ?? ResolveFileEndings(name);
            var asset = (T)Activator.CreateInstance(typeof(T), [param]);
            _Assets.Add(name, asset);
            NLogixFx.Debug(
                $"[AssetLoader<{typeof(T).Name}>] Loaded asset '{name}' successfully from " +
                $"{(rawData != null ? "rawData" : param)}");

            return asset;
        }
        catch (Exception ex)
        {
            NLogixFx.Error(
                $"[AssetLoader<{typeof(T).Name}>] Failed to load asset '{name}'. " +
                $"Input: {(param ?? "null")}. Error: {ex.Message}\n{ex}");
            if (Debug) throw;
        }
        return null;
    }

    /// <summary>
    /// Loads all compatible files in the root directory.
    /// </summary>
    /// <param name="logErrors">Determines if errors should be logged</param>
    /// <returns>Array containing the names of all successfully loaded files</returns>
    public virtual String[] LoadAllFilesInDirectory(bool logErrors = false)
    {
        var assetNames = new List<String>();
        foreach (var file in Directory.EnumerateFiles(RootFolder))
        {
            try
            {
                T asset = (T)Activator.CreateInstance(typeof(T), [file]);
                String name = Path.GetFileNameWithoutExtension(file);
                _Assets.Add(name, asset);
                assetNames.Add(name);

                if (Debug)
                {
                    NLogixFx.Info($"[AssetLoader<{typeof(T).Name}>] Loaded asset: '{name}' from file: '{file}'");
                }
            }
            catch (Exception e)
            {
                if (logErrors)
                {
                    NLogixFx.Error($"""
                    [AssetLoader<{typeof(T).Name}>] Failed to load asset from file: '{file}'
                    Reason: {e.GetType().Name} - {e.Message}
                    """);
                }
                if (Debug) throw;
            }
        }
        return [.. assetNames];
    }

    /// <summary>
    /// Resolves the file endings.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>Filename + proper file ending or the original String in case no file could be found.</returns>
    private String ResolveFileEndings(String name)
    {
        foreach (var ending in _FileEndings)
        {
            var candidate = Path.Combine(RootFolder, $"{name}{ending}");
            if (File.Exists(candidate))
                return candidate;
        }

        var attemptedPaths = _FileEndings
            .Select(f => Path.Combine(RootFolder, $"{name}{f}"))
            .ToArray();

        NLogixFx.Warn($"""
        [AssetLoader] Could not find a matching file for asset '{name}'.
        Tried extensions: {String.Join(", ", _FileEndings)}
        Attempted paths:
        {String.Join("\n", attemptedPaths)}
        Root folder: {RootFolder}
        Fallback path used: {Path.Combine(RootFolder, name)}
        """);

        return Path.Combine(RootFolder, name);
    }

    /// <summary>
    /// Unloads the Asset with the given name
    /// </summary>
    /// <param name="name">Name of the Asset</param>
    public void Release(String name)
    {
        ObjectDisposedException.ThrowIf(Disposed, nameof(AssetLoader<T>));
        if (!_Assets.TryGetValue(name, out T value)) return;

        value.Dispose();
        _Assets.Remove(name);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
        Disposed = true;

        foreach (var kvp in _Assets)
        {
            try
            {
                kvp.Value.Dispose();
            }
            catch (Exception e)
            {
                NLogixFx.Error(
                    $"[AssetLoader<{typeof(T).Name}>] Failed to dispose asset '{kvp.Key}'. " +
                    $"Error: {e.Message}\n{e}");
            }
        }
        _Assets.Clear();
    }
}
