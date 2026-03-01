// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Environment;
using Nalix.Framework.Configuration.Binding;
using Nalix.Framework.Configuration.Internal;
using Nalix.Framework.Injection;
using Nalix.Framework.Injection.DI;

namespace Nalix.Framework.Configuration;

/// <summary>
/// A singleton that provides access to configuration value containers with optimized performance
/// for high-throughput real-time server applications.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides thread-safe access to configuration containers through the following mechanisms:
/// - <see cref="Get{TClass}()"/> uses <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/> for thread-safe container retrieval and creation.
/// - <see cref="ReloadAll"/> uses a <see cref="System.Threading.ReaderWriterLockSlim"/> write lock to ensure exclusive access during reload operations.
/// - All public methods are safe to call concurrently from multiple threads.
/// </para>
/// <para>
/// Performance characteristics:
/// - Container retrieval from cache is lock-free and highly scalable.
/// - First-time container creation is synchronized per type.
/// - Reload operations block all configuration access during the reload.
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("ConfigFilePath = {ConfigFilePath,nq}, LoadedTypes = {_configContainerDict.Count}")]
public sealed class ConfigurationManager : SingletonBase<ConfigurationManager>
{
    #region Fields

    private System.Lazy<IniConfig> _iniFile;
    private readonly System.String _baseConfigDirectory;
    private readonly System.Threading.ReaderWriterLockSlim _configLock;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, ConfigurationLoader> _configContainerDict;

    private System.Int32 _isReloading;
    private System.Boolean _directoryChecked;
    private System.String _configFilePath;
    private System.IO.FileSystemWatcher? _configFileWatcher;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    /// <remarks>
    /// This property is thread-safe. Use <see cref="SetConfigFilePath"/> to change the path.
    /// </remarks>
    public System.String ConfigFilePath
    {
        get
        {
            _configLock.EnterReadLock();
            try
            {
                return _configFilePath;
            }
            finally
            {
                _configLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the configuration file exists.
    /// </summary>
    public System.Boolean ConfigFileExists => _iniFile.IsValueCreated && _iniFile.Value.ExistsFile;

    /// <summary>
    /// Gets the last reload timestamp.
    /// </summary>
    public System.DateTime LastReloadTime { get; private set; } = System.DateTime.UtcNow;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationManager"/> class.
    /// </summary>
    public ConfigurationManager()
    {
        // Determine the configuration directory with validation
        System.String configDirectory = Directories.ConfigurationDirectory;

        // Validate the directory path for security
        if (System.String.IsNullOrWhiteSpace(configDirectory))
        {
            throw new System.InvalidOperationException("Configuration directory cannot be null or empty.");
        }

        // Get the full path to prevent path traversal attacks
        _baseConfigDirectory = System.IO.Path.GetFullPath(configDirectory);

        // Initialize with default configuration file
        _configFilePath = System.IO.Path.Combine(_baseConfigDirectory, "default.ini");

        // Validate the initial path
        VALIDATE_CONFIG_PATH(_configFilePath);

        // Lazy-load the INI file to defer file access until needed
        _iniFile = CREATE_LAZY_INI_CONFIG(_configFilePath);
        SETUP_FILE_WATCHER();

        _configContainerDict = new();
        _configLock = new(System.Threading.LockRecursionPolicy.NoRecursion);
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Sets a new configuration file path and optionally reloads all configurations.
    /// </summary>
    /// <param name="newConfigFilePath">The new configuration file path.</param>
    /// <param name="autoReload">
    /// If <see langword="true"/>, automatically reloads all configurations from the new file.
    /// Default is <see langword="true"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the path was changed successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="newConfigFilePath"/> is null or whitespace.
    /// </exception>
    /// <exception cref="System.Security.SecurityException">
    /// Thrown when <paramref name="newConfigFilePath"/> is outside the allowed configuration directory.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is thread-safe and will block all configuration access during the path change.
    /// </para>
    /// <para>
    /// Security: The new path must be within the base configuration directory to prevent
    /// directory traversal attacks.
    /// </para>
    /// <para>
    /// If <paramref name="autoReload"/> is <see langword="true"/>, all existing configuration
    /// containers will be reinitialized from the new file. If <see langword="false"/>, you must
    /// manually call <see cref="ReloadAll"/> to load configurations from the new file.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.Boolean SetConfigFilePath(System.String newConfigFilePath, System.Boolean autoReload = true)
    {
        // Validate input
        if (System.String.IsNullOrWhiteSpace(newConfigFilePath))
        {
            throw new System.ArgumentException(
                "Configuration file path cannot be null or whitespace.",
                nameof(newConfigFilePath));
        }

        // Normalize and validate the new path
        System.String normalizedPath = System.IO.Path.GetFullPath(newConfigFilePath);
        VALIDATE_CONFIG_PATH(normalizedPath);

        // Ensure only one path change happens at a time
        if (System.Threading.Interlocked.Exchange(ref _isReloading, 1) == 1)
        {
            return false;
        }

        try
        {
            _configLock.EnterWriteLock();
            try
            {
                // Check if the path is actually different
                if (System.String.Equals(_configFilePath, normalizedPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Path is the same, no change needed
                }

                // Flush the old file if it was loaded
                if (_iniFile.IsValueCreated)
                {
                    try
                    {
                        _iniFile.Value.Flush();
                    }
                    catch
                    {
                        // Ignore flush errors on old file
                    }
                }

                // Update the path
                System.String oldPath = _configFilePath;
                _configFilePath = normalizedPath;

                // Reset directory check flag
                _directoryChecked = false;

                // Create new lazy INI file instance
                _iniFile = CREATE_LAZY_INI_CONFIG(_configFilePath);
                SETUP_FILE_WATCHER();

                // Log the change
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Info($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                          $"path-changed from='{oldPath}' to='{normalizedPath}'");

                // Optionally reload all configurations
                if (autoReload && !_configContainerDict.IsEmpty)
                {
                    try
                    {
                        // Force load the new INI file
                        IniConfig newIniFile = _iniFile.Value;

                        // Reinitialize all existing containers
                        foreach (ConfigurationLoader container in _configContainerDict.Values)
                        {
                            container.Initialize(newIniFile);
                        }

                        LastReloadTime = System.DateTime.UtcNow;

                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Info($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                                                      $"auto-reload-ok count={_configContainerDict.Count}");
                    }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                                                       $"auto-reload-fail msg={ex.Message}", ex);

                        // Rollback the path change on reload failure
                        _configFilePath = oldPath;
                        _iniFile = CREATE_LAZY_INI_CONFIG(oldPath);
                        SETUP_FILE_WATCHER();

                        return false;
                    }
                }

                return true;
            }
            finally
            {
                _configLock.ExitWriteLock();
            }
        }
        finally
        {
            _ = System.Threading.Interlocked.Exchange(ref _isReloading, 0);
        }
    }

    /// <summary>
    /// Initializes if needed and returns an instance of <typeparamref name="TClass"/>.
    /// </summary>
    /// <typeparam name="TClass">The type of the configuration container.</typeparam>
    /// <returns>An instance of type <typeparamref name="TClass"/>.</returns>
    /// <remarks>
    /// This method is thread-safe. The first call for a given type will create and initialize the container.
    /// Subsequent calls will return the cached instance. Initialization happens outside of locks to prevent
    /// potential deadlocks if the container initialization requires additional resources.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public TClass Get<TClass>() where TClass : ConfigurationLoader, new()
    {
        return (TClass)_configContainerDict.GetOrAdd(typeof(TClass), _ =>
        {
            TClass container = new();

            // Get the INI file reference first (thread-safe via Lazy<T>)
            IniConfig iniFile = _iniFile.Value;

            // Initialize the container outside of any explicit lock
            // ConcurrentDictionary.GetOrAdd ensures only one initialization per type
            container.Initialize(iniFile);

            return container;
        });
    }

    /// <summary>
    /// Initializes if needed and returns an instance of <typeparamref name="TClass"/>.
    /// </summary>
    /// <typeparam name="TClass">The type of the configuration container.</typeparam>
    /// <returns>An instance of type <typeparamref name="TClass"/>.</returns>
    /// <param name="configFilePath">The new configuration file path.</param>
    /// <param name="autoReload">
    /// If <see langword="true"/>, automatically reloads all configurations from the new file.
    /// Default is <see langword="true"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the path was changed successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="configFilePath"/> is null or whitespace.
    /// </exception>
    /// <exception cref="System.Security.SecurityException">
    /// Thrown when <paramref name="configFilePath"/> is outside the allowed configuration directory.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is thread-safe and will block all configuration access during the path change.
    /// </para>
    /// <para>
    /// Security: The new path must be within the base configuration directory to prevent
    /// directory traversal attacks.
    /// </para>
    /// <para>
    /// If <paramref name="autoReload"/> is <see langword="true"/>, all existing configuration
    /// containers will be reinitialized from the new file. If <see langword="false"/>, you must
    /// manually call <see cref="ReloadAll"/> to load configurations from the new file.
    /// </para>
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public TClass Get<TClass>(System.String configFilePath, System.Boolean autoReload = true) where TClass : ConfigurationLoader, new()
    {
        this.SetConfigFilePath(configFilePath, autoReload);
        return (TClass)_configContainerDict.GetOrAdd(typeof(TClass), _ =>
        {
            TClass container = new();

            // Get the INI file reference first (thread-safe via Lazy<T>)
            IniConfig iniFile = _iniFile.Value;

            // Initialize the container outside of any explicit lock
            // ConcurrentDictionary.GetOrAdd ensures only one initialization per type
            container.Initialize(iniFile);

            return container;
        });
    }

    /// <summary>
    /// Reloads all configuration containers from the INI file.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the reload was successful;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method is thread-safe but will block concurrent reload attempts.
    /// Only one reload operation can execute at a time. During reload, configuration
    /// access via <see cref="Get{TClass}()"/> may be briefly blocked.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.Boolean ReloadAll()
    {
        // Ensure only one reload happens at a time
        if (System.Threading.Interlocked.Exchange(ref _isReloading, 1) == 1)
        {
            return false;
        }

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

                LastReloadTime = System.DateTime.UtcNow;
                return true;
            }
            finally
            {
                _configLock.ExitWriteLock();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[FW.{nameof(ConfigurationManager)}:{nameof(ReloadAll)}] " +
                                              $"reload-ok count={_configContainerDict.Count}");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[FW.{nameof(ConfigurationManager)}:{nameof(ReloadAll)}] " +
                                           $"reload-fail msg={ex.Message}", ex);
            return false;
        }
        finally
        {
            _ = System.Threading.Interlocked.Exchange(ref _isReloading, 0);
        }
    }

    /// <summary>
    /// Checks if a specific configuration type is loaded.
    /// </summary>
    /// <typeparam name="TClass">The configuration type to check.</typeparam>
    /// <returns>
    /// <see langword="true"/> if the configuration is loaded;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method is thread-safe and lock-free.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean IsLoaded<TClass>() where TClass : ConfigurationLoader =>
        _configContainerDict.ContainsKey(typeof(TClass));

    /// <summary>
    /// Removes a specific configuration from the cache.
    /// </summary>
    /// <typeparam name="TClass">The configuration type to remove.</typeparam>
    /// <returns>
    /// <see langword="true"/> if the configuration was removed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method is thread-safe. Removing a configuration will cause it to be
    /// reinitialized on the next call to <see cref="Get{TClass}()"/>.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.Boolean Remove<TClass>() where TClass : ConfigurationLoader => _configContainerDict.TryRemove(typeof(TClass), out _);

    /// <summary>
    /// Clears all cached configurations.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe. All configurations will be reinitialized on
    /// the next call to <see cref="Get{TClass}()"/> for each type.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void ClearAll() => _configContainerDict.Clear();

    /// <summary>
    /// Ensures that changes are written to disk.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and will flush pending changes to the INI file.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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

    #endregion Public Methods

    #region Protected Methods

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    protected override void DisposeManaged()
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

        _configFileWatcher?.Dispose();
        _configFileWatcher = null;

        // Clean up resources
        _configLock.Dispose();
        _configContainerDict.Clear();

        // DO NOT call Dispose() here - it will cause infinite recursion
        // The base class will handle the disposal chain
    }

    #endregion Protected Methods

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void SETUP_FILE_WATCHER()
    {
        // Dispose watcher cũ nếu có
        _configFileWatcher?.Dispose();
        _configFileWatcher = null;

        System.String? directory = System.IO.Path.GetDirectoryName(_configFilePath);
        System.String file = System.IO.Path.GetFileName(_configFilePath);

        if (directory == null || file == null)
        {
            return;
        }

        _configFileWatcher = new System.IO.FileSystemWatcher(directory, file)
        {
            NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size
        };
        _configFileWatcher.Changed += (s, e) =>
        {
            if (e.FullPath.Equals(_configFilePath, System.StringComparison.OrdinalIgnoreCase))
            {
                ReloadAll();
            }
        };
        _configFileWatcher.EnableRaisingEvents = true;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Lazy<IniConfig> CREATE_LAZY_INI_CONFIG(System.String filePath)
    {
        return new System.Lazy<IniConfig>(() =>
        {
            // Ensure the directory exists before trying to access the file
            this.ENSURE_CONFIG_DIRECTORY_EXISTS();
            return new IniConfig(filePath);
        }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void VALIDATE_CONFIG_PATH(System.String pathToValidate)
    {
        // Normalize both paths for comparison
        var normalizedPath = System.IO.Path.GetFullPath(pathToValidate);
        var normalizedBaseDir = System.IO.Path.GetFullPath(_baseConfigDirectory);

        // Ensure base directory ends with directory separator for accurate prefix matching
        if (!normalizedBaseDir.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
        {
            normalizedBaseDir += System.IO.Path.DirectorySeparatorChar;
        }

        if (!normalizedPath.StartsWith(normalizedBaseDir, System.StringComparison.OrdinalIgnoreCase))
        {
            throw new System.Security.SecurityException(
                $"Configuration file path '{pathToValidate}' is outside the allowed configuration directory '{_baseConfigDirectory}'.");
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ENSURE_CONFIG_DIRECTORY_EXISTS()
    {
        if (!_directoryChecked)
        {
            // Get the directory from the current config file path
            System.String? directory = System.IO.Path.GetDirectoryName(_configFilePath);

            if (System.String.IsNullOrWhiteSpace(directory))
            {
                throw new System.InvalidOperationException(
                    "Configuration file path does not contain a valid directory component.");
            }

            if (!System.IO.Directory.Exists(directory))
            {
                try
                {
                    System.IO.DirectoryInfo dirInfo = System.IO.Directory.CreateDirectory(directory);

                    // Verify directory was actually created and is accessible
                    if (!dirInfo.Exists)
                    {
                        throw new System.InvalidOperationException(
                            $"Directory creation reported success but directory does not exist: {directory}");
                    }
                }
                catch (System.UnauthorizedAccessException ex)
                {
                    throw new System.UnauthorizedAccessException(
                        $"Access denied when creating configuration directory: {directory}", ex);
                }
                catch (System.IO.PathTooLongException ex)
                {
                    throw new System.IO.PathTooLongException(
                        $"Configuration directory path is too long: {directory}", ex);
                }
                catch (System.IO.IOException ex)
                {
                    throw new System.IO.IOException(
                        $"I/O error creating configuration directory: {directory}", ex);
                }
                catch (System.Exception ex)
                {
                    throw new System.InvalidOperationException(
                        $"Unexpected error creating configuration directory: {directory}", ex);
                }
            }

            _directoryChecked = true;
        }
    }

    #endregion Private Methods
}