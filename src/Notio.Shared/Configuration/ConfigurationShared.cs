using Notio.Defaults;
using Notio.Shared.Injection.DI;
using Notio.Shared.Internal;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notio.Shared.Configuration;

/// <summary>
/// A singleton that provides access to configuration value containers with optimized performance
/// for high-throughput real-time server applications.
/// </summary>
/// <remarks>
/// This implementation includes thread safety, caching optimizations, and lazy loading.
/// </remarks>
public sealed class ConfigurationStore : SingletonBase<ConfigurationStore>
{
    #region Fields

    private readonly ConcurrentDictionary<Type, ConfigurationBinder> _configContainerDict = new();
    private readonly ReaderWriterLockSlim _configLock = new(LockRecursionPolicy.NoRecursion);
    private readonly Lazy<ConfiguredIniFile> _iniFile;

    private int _isReloading;
    private bool _directoryChecked;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public string ConfigFilePath { get; }

    /// <summary>
    /// Gets a value indicating whether the configuration file exists.
    /// </summary>
    public bool ConfigFileExists => _iniFile.IsValueCreated && _iniFile.Value.ExistsFile;

    /// <summary>
    /// Gets the last reload timestamp.
    /// </summary>
    public DateTime LastReloadTime { get; private set; } = DateTime.UtcNow;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationStore"/> class.
    /// </summary>
    private ConfigurationStore()
    {
        // Determine the configuration file path
        this.ConfigFilePath = Path.Combine(DefaultDirectories.ConfigPath, "Configured.ini");

        // Lazy-load the INI file to defer file access until needed
        _iniFile = new Lazy<ConfiguredIniFile>(() =>
        {
            // Ensure the directory exists before trying to access the file
            this.EnsureConfigDirectoryExists();
            return new ConfiguredIniFile(ConfigFilePath);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes if needed and returns an instance of <typeparamref name="TClass"/>.
    /// </summary>
    /// <typeparam name="TClass">The type of the configuration container.</typeparam>
    /// <returns>An instance of type <typeparamref name="TClass"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TClass Get<TClass>() where TClass : ConfigurationBinder, new()
    {
        return (TClass)_configContainerDict.GetOrAdd(typeof(TClass), type =>
        {
            var container = new TClass();

            // Initialize the container with the INI file
            _configLock.EnterReadLock();
            try
            {
                container.Initialize(_iniFile.Value);
            }
            finally
            {
                _configLock.ExitReadLock();
            }

            return container;
        });
    }

    /// <summary>
    /// Reloads all configuration containers from the INI file.
    /// </summary>
    /// <returns>True if the reload was successful; otherwise, false.</returns>
    public bool ReloadAll()
    {
        // Ensure only one reload happens at a time
        if (Interlocked.Exchange(ref _isReloading, 1) == 1)
            return false;

        try
        {
            _configLock.EnterWriteLock();
            try
            {
                // Reload the INI file
                if (_iniFile.IsValueCreated)
                {
                    _iniFile.Value.Reload();
                }

                // Reinitialize all containers
                foreach (var container in _configContainerDict.Values)
                {
                    container.Initialize(_iniFile.Value);
                }

                LastReloadTime = DateTime.UtcNow;
                return true;
            }
            finally
            {
                _configLock.ExitWriteLock();
            }
        }
        catch (Exception)
        {
            // In production, log the exception
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _isReloading, 0);
        }
    }

    /// <summary>
    /// Checks if a specific configuration type is loaded.
    /// </summary>
    /// <typeparam name="TClass">The configuration type to check.</typeparam>
    /// <returns>True if the configuration is loaded; otherwise, false.</returns>
    public bool IsLoaded<TClass>() where TClass : ConfigurationBinder
        => _configContainerDict.ContainsKey(typeof(TClass));

    /// <summary>
    /// Removes a specific configuration from the cache.
    /// </summary>
    /// <typeparam name="TClass">The configuration type to remove.</typeparam>
    /// <returns>True if the configuration was removed; otherwise, false.</returns>
    public bool Remove<TClass>() where TClass : ConfigurationBinder
    {
        _configLock.EnterWriteLock();
        try
        {
            return _configContainerDict.TryRemove(typeof(TClass), out _);
        }
        finally
        {
            _configLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all cached configurations.
    /// </summary>
    public void ClearAll()
    {
        _configLock.EnterWriteLock();
        try
        {
            _configContainerDict.Clear();
        }
        finally
        {
            _configLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Ensures that changes are written to disk.
    /// </summary>
    public void Flush()
    {
        if (_iniFile.IsValueCreated)
        {
            _configLock.EnterReadLock();
            try
            {
                _iniFile.Value.Flush();
            }
            finally
            {
                _configLock.ExitReadLock();
            }
        }
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Flush any pending changes
            if (_iniFile.IsValueCreated)
            {
                try
                {
                    _iniFile.Value.Flush();
                }
                catch
                {
                    // Ignore exceptions during cleanup
                }
            }

            // Clean up resources
            _configLock.Dispose();
            _configContainerDict.Clear();
        }

        base.Dispose(disposing);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Ensures the configuration directory exists.
    /// </summary>
    private void EnsureConfigDirectoryExists()
    {
        if (!_directoryChecked)
        {
            string? directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to create configuration directory: {directory}", ex);
                }
            }

            _directoryChecked = true;
        }
    }

    #endregion
}
