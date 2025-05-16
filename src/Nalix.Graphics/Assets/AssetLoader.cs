using Nalix.Logging.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Nalix.Graphics.Assets;

/// <summary>
/// Asset management class. Handles loading/unloading of assets located in a specified root folder.
/// Multiple instances of the AssetManager class can be used to simplify asset memory management.
/// </summary>
public abstract class AssetLoader<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>
    : IDisposable where T : class, IDisposable
{
    /// <summary>
    /// List of supported file endings for this AssetLoader
    /// </summary>
    protected string[] _FileEndings;

    /// <summary>
    /// Dictionary of loaded assets, where the key is the asset name and the value is the asset instance.
    /// </summary>
    protected Dictionary<string, T> _Assets = [];

    /// <summary>
    /// List of supported file endings for this AssetLoader
    /// </summary>
    public IEnumerable<string> FileEndings => _FileEndings;

    /// <summary>
    /// The root folder where assets are located.
    /// </summary>
    public string RootFolder { get; set; }

    /// <summary>
    /// Indicates whether the asset loader should log debug information.
    /// </summary>
    public bool Debug { get; set; }

    /// <summary>
    /// Indicates whether the asset loader has been disposed.
    /// </summary>
    public bool Disposed { get; private set; }

    internal AssetLoader(IEnumerable<string> supportedFileEndings, string assetRoot = "")
    {
        RootFolder = assetRoot;
        _FileEndings = [.. new[] { string.Empty }.Concat(supportedFileEndings).Distinct()];
    }

    /// <summary>
    /// Finalizer for the AssetLoader class. Calls Dispose(false) to release unmanaged resources.
    /// </summary>
    ~AssetLoader() => Dispose(false);

    /// <summary>
    /// Loads or retrieves an already loaded instance of T from a File or Raw Data Source
    /// </summary>
    /// <param name="name"></param>
    /// <param name="rawData"></param>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual T Load(string name, byte[] rawData = null)
    {
        ObjectDisposedException.ThrowIf(Disposed, nameof(AssetLoader<T>));
        ArgumentNullException.ThrowIfNull(name);

        if (_Assets.TryGetValue(name, out T value)) return value;

        string input = null;
        try
        {
            T asset;

            if (rawData != null)
            {
                asset = CreateInstanceFromRawData(rawData);
                input = "rawData";
            }
            else
            {
                input = ResolveFileEndings(name);
                asset = CreateInstanceFromPath(input);
            }

            _Assets.Add(name, asset);

            ($"[AssetLoader<{typeof(T).Name}>] Loaded asset '{name}' successfully from {input}").Debug();
            return asset;
        }
        catch (Exception ex)
        {
            ($"[AssetLoader<{typeof(T).Name}>] Failed to load asset '{name}'. Input: {input ?? "null"}. Error: {ex.Message}\n{ex}").Error();
            if (Debug) throw;
        }

        return null;
    }

    /// <summary>
    /// Loads all files in the specified directory and adds them to the asset manager.
    /// </summary>
    /// <param name="logErrors"></param>
    /// <returns></returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual string[] LoadAllFilesInDirectory(bool logErrors = false)
    {
        var assetNames = new List<string>();

        foreach (var file in Directory.EnumerateFiles(RootFolder))
        {
            try
            {
                T asset = CreateInstanceFromPath(file);
                string name = Path.GetFileNameWithoutExtension(file);
                _Assets.Add(name, asset);
                assetNames.Add(name);

                if (Debug)
                    $"[AssetLoader<{typeof(T).Name}>] Loaded asset: '{name}' from file: '{file}'".Info();
            }
            catch (Exception e)
            {
                if (logErrors)
                {
                    $"""
                    [AssetLoader<{typeof(T).Name}>] Failed to load asset from file: '{file}'
                    Reason: {e.GetType().Name} - {e.Message}
                    """.Error();
                }

                if (Debug) throw;
            }
        }

        return [.. assetNames];
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string ResolveFileEndings(string name)
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

        $"""
        [AssetLoader] Could not find a matching file for asset '{name}'.
        Tried extensions: {string.Join(", ", _FileEndings)}
        Attempted paths:
        {string.Join("\n", attemptedPaths)}
        Root folder: {RootFolder}
        Fallback path used: {Path.Combine(RootFolder, name)}
        """.Warn();

        return Path.Combine(RootFolder, name);
    }

    /// <summary>
    /// Releases the asset with the specified name.
    /// </summary>
    /// <param name="name"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Release(string name)
    {
        ObjectDisposedException.ThrowIf(Disposed, nameof(AssetLoader<T>));
        if (!_Assets.TryGetValue(name, out T value)) return;

        value.Dispose();
        _Assets.Remove(name);
    }

    /// <summary>
    /// Disposes the asset loader and all loaded assets.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the asset loader and all loaded assets.
    /// </summary>
    /// <param name="disposing"></param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
                ($"[AssetLoader<{typeof(T).Name}>] Failed to dispose asset '{kvp.Key}'. Error: {e.Message}\n{e}").Error();
            }
        }

        _Assets.Clear();
    }

    /// <summary>
    /// Creates an instance of type <typeparamref name="T"/> from raw binary data.
    /// </summary>
    /// <param name="rawData">The raw byte array representing the asset data.</param>
    /// <returns>An instance of <typeparamref name="T"/> created from the raw data.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if this type <typeparamref name="T"/> does not support loading from raw data.
    /// Override this method in a derived class to provide a valid implementation.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected virtual T CreateInstanceFromRawData(byte[] rawData)
    {
        throw new NotSupportedException($"{typeof(T).Name} does not support loading from raw data. Override this method.");
    }

    /// <summary>
    /// Creates an instance of type <typeparamref name="T"/> from a file path.
    /// </summary>
    /// <param name="path">The full or relative file path to the asset.</param>
    /// <returns>
    /// An instance of <typeparamref name="T"/> created using a constructor that accepts a single <see cref="string"/> argument.
    /// </returns>
    /// <exception cref="MissingMethodException">
    /// Thrown if <typeparamref name="T"/> does not have a public constructor accepting a <see cref="string"/> path.
    /// </exception>
    /// <exception cref="TargetInvocationException">
    /// Thrown if the constructor of <typeparamref name="T"/> throws an exception.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected virtual T CreateInstanceFromPath(string path)
        => (T)Activator.CreateInstance(typeof(T), [path]);
}
