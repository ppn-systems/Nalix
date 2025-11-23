// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Environment;
using Nalix.Common.Exceptions;
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
/// Thread-safety model:
/// - <see cref="Get{TClass}()"/> snapshots <c>_iniFile</c> under a read lock before use.
/// - <see cref="ReloadAll"/> and <see cref="SetConfigFilePath"/> use a write lock for exclusive access.
/// - A <see cref="System.Threading.SemaphoreSlim"/>(1,1) gate serialises reload/path-change operations
///   and makes callers WAIT instead of silently returning <see langword="false"/>.
/// - <see cref="System.IO.FileSystemWatcher"/> changes are debounced (300 ms) to absorb the
///   multiple rapid events that most OS file-writers emit for a single logical write.
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("ConfigFilePath = {ConfigFilePath,nq}, LoadedTypes = {_configContainerDict.Count}")]
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class ConfigurationManager : SingletonBase<ConfigurationManager>
{
    #region Fields

    // volatile: assigned atomically in SetConfigFilePath; all threads must see the latest reference.
    private volatile System.Lazy<IniConfig> _iniFile;
    private readonly System.String _baseConfigDirectory;
    private readonly System.Threading.ReaderWriterLockSlim _configLock;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Lazy<ConfigurationLoader>> _configContainerDict;

    // SemaphoreSlim(1,1): serialises ReloadAll / SetConfigFilePath.
    // Using Wait() instead of Interlocked.Exchange means callers BLOCK until the current
    // operation completes — fixing the test failure where FileSystemWatcher would grab the
    // old Interlocked flag just before the test's manual ReloadAll() call.
    private readonly System.Threading.SemaphoreSlim _reloadGate;

    // Debounce: FileSystemWatcher fires 2-3 Changed events per single file write on most OSes.
    // We reset a one-shot timer on every event; only the last one fires ReloadAll().
    private System.Threading.Timer? _debounceTimer;
    private static readonly System.TimeSpan _debounceDelay = System.TimeSpan.FromMilliseconds(300);

    // volatile: read/written from multiple threads without a full lock.
    private volatile System.Boolean _directoryChecked;

    private System.String _configFilePath;
    private System.IO.FileSystemWatcher? _configFileWatcher;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the path to the active configuration file.
    /// </summary>
    public System.String ConfigFilePath
    {
        get
        {
            _configLock.EnterReadLock();
            try { return _configFilePath; }
            finally { _configLock.ExitReadLock(); }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the configuration file exists on disk.
    /// </summary>
    public System.Boolean ConfigFileExists
    {
        get
        {
            // Snapshot under read lock to avoid a TOCTOU race with SetConfigFilePath.
            System.Lazy<IniConfig> snapshot;
            _configLock.EnterReadLock();
            try { snapshot = _iniFile; }
            finally { _configLock.ExitReadLock(); }

            return snapshot.IsValueCreated && snapshot.Value.ExistsFile;
        }
    }

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
        System.String configDirectory = Directories.ConfigurationDirectory;

        if (System.String.IsNullOrWhiteSpace(configDirectory))
        {
            throw new System.InvalidOperationException("Configuration directory cannot be null or empty.");
        }

        _baseConfigDirectory = System.IO.Path.GetFullPath(configDirectory);
        _configFilePath = System.IO.Path.Combine(_baseConfigDirectory, "default.ini");

        VALIDATE_CONFIG_PATH(_configFilePath);

        // Initialise synchronisation primitives BEFORE the watcher.
        // If the watcher fires during construction it needs _configLock and _reloadGate to exist.
        _configLock = new(System.Threading.LockRecursionPolicy.NoRecursion);
        _reloadGate = new System.Threading.SemaphoreSlim(1, 1);
        _configContainerDict = new();

        _iniFile = CREATE_LAZY_INI_CONFIG(_configFilePath);
        SETUP_FILE_WATCHER();
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Changes the active configuration file path and optionally reloads all containers.
    /// </summary>
    /// <param name="newConfigFilePath">Absolute or relative path to the new INI file.</param>
    /// <param name="autoReload">
    /// When <see langword="true"/> (default) all already-initialised containers are reloaded
    /// from the new file immediately. When <see langword="false"/> you must call
    /// <see cref="ReloadAll"/> manually.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; <see langword="false"/> if the path was unchanged,
    /// the gate timed out, or an auto-reload failed (path is rolled back in that case).
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.Boolean SetConfigFilePath(System.String newConfigFilePath, System.Boolean autoReload = true)
    {
        if (System.String.IsNullOrWhiteSpace(newConfigFilePath))
        {
            throw new System.ArgumentException(
                "Configuration file path cannot be null or whitespace.",
                nameof(newConfigFilePath));
        }

        System.String normalizedPath = System.IO.Path.GetFullPath(newConfigFilePath);
        VALIDATE_CONFIG_PATH(normalizedPath);

        // Wait up to 5 s for any concurrent reload/path-change to finish.
        if (!_reloadGate.Wait(System.TimeSpan.FromSeconds(5)))
        {
            return false;
        }

        System.Boolean success = false;
        System.String? pathToWatch = null;

        try
        {
            _configLock.EnterWriteLock();
            try
            {
                if (System.String.Equals(_configFilePath, normalizedPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    return false; // no-op
                }

                if (_iniFile.IsValueCreated)
                {
                    try { _iniFile.Value.Flush(); } catch { /* ignore flush errors on the old file */ }
                }

                System.String oldPath = _configFilePath;
                _configFilePath = normalizedPath;
                _directoryChecked = false;
                _iniFile = CREATE_LAZY_INI_CONFIG(_configFilePath);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Info($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                          $"path-changed from='{oldPath}' to='{normalizedPath}'");

                if (autoReload && !_configContainerDict.IsEmpty)
                {
                    try
                    {
                        IniConfig newIniFile = _iniFile.Value; // force-load the new file

                        foreach (var lazy in _configContainerDict.Values)
                        {
                            if (lazy.IsValueCreated)
                            {
                                lazy.Value.Initialize(newIniFile);
                            }
                        }

                        LastReloadTime = System.DateTime.UtcNow;

                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Info($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                                                      $"auto-reload-ok count={_configContainerDict.Count}");

                        pathToWatch = normalizedPath;
                        success = true;
                    }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                            .Error($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                                   $"auto-reload-fail msg={ex.Message}", ex);

                        // Roll back state — do NOT call SETUP_FILE_WATCHER inside the write lock.
                        _configFilePath = oldPath;
                        _directoryChecked = false;
                        _iniFile = CREATE_LAZY_INI_CONFIG(oldPath);

                        pathToWatch = oldPath; // restore watcher for the old path
                        success = false;
                    }
                }
                else
                {
                    pathToWatch = normalizedPath;
                    success = true;
                }
            }
            finally
            {
                _configLock.ExitWriteLock();
            }

            // Set up the watcher AFTER releasing the write lock (both success and rollback paths).
            if (pathToWatch is not null)
            {
                SETUP_FILE_WATCHER();
            }

            return success;
        }
        finally
        {
            _reloadGate.Release();
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
        System.Lazy<IniConfig> iniSnapshot;
        _configLock.EnterReadLock();
        try { iniSnapshot = _iniFile; }
        finally { _configLock.ExitReadLock(); }

        System.Lazy<ConfigurationLoader> lazy = _configContainerDict.GetOrAdd(
            typeof(TClass),
            _ => new System.Lazy<ConfigurationLoader>(() =>
            {
                TClass container = new();
                container.Initialize(iniSnapshot.Value);
                return container;
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)
        );

        return (TClass)lazy.Value;
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
    public TClass Get<TClass>(System.String configFilePath, System.Boolean autoReload = true)
        where TClass : ConfigurationLoader, new()
    {
        this.SetConfigFilePath(configFilePath, autoReload);
        return Get<TClass>();
    }

    /// <summary>
    /// Reloads every already-initialised configuration container from disk.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> on success; <see langword="false"/> if the gate timed out
    /// or an exception occurred during reload.
    /// </returns>
    /// <remarks>
    /// Callers block (up to 5 s) while a concurrent reload is in progress instead of
    /// receiving a silent <see langword="false"/>. This is the key fix for the test failure:
    /// <see cref="System.IO.FileSystemWatcher"/> can start a background reload right before
    /// the test calls <see cref="ReloadAll"/> manually; the old <c>Interlocked</c> flag would
    /// cause the manual call to return <see langword="false"/> immediately.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public System.Boolean ReloadAll()
    {
        if (!_reloadGate.Wait(System.TimeSpan.FromSeconds(5)))
        {
            return false;
        }

        System.Boolean reloadSuccess = false;
        System.Exception? reloadException = null;

        try
        {
            _configLock.EnterWriteLock();
            try
            {
                if (_iniFile.IsValueCreated)
                {
                    _iniFile.Value.Reload();
                }

                foreach (var lazy in _configContainerDict.Values)
                {
                    if (lazy.IsValueCreated)
                    {
                        lazy.Value.Initialize(_iniFile.Value);
                    }
                }

                LastReloadTime = System.DateTime.UtcNow;
                reloadSuccess = true;
            }
            catch (System.Exception ex)
            {
                reloadException = ex;
            }
            finally
            {
                // Release write lock BEFORE logging — avoids holding it longer than necessary
                // and prevents a deadlock if the logger calls back into the manager.
                _configLock.ExitWriteLock();
            }

            if (reloadSuccess)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Info($"[FW.{nameof(ConfigurationManager)}:{nameof(ReloadAll)}] " +
                          $"reload-ok count={_configContainerDict.Count}");
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Error($"[FW.{nameof(ConfigurationManager)}:{nameof(ReloadAll)}] " +
                           $"reload-fail msg={reloadException?.Message}", reloadException);
            }

            return reloadSuccess;
        }
        finally
        {
            _reloadGate.Release();
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
    public System.Boolean Remove<TClass>() where TClass : ConfigurationLoader =>
        _configContainerDict.TryRemove(typeof(TClass), out _);

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
        // Snapshot under read lock — avoids TOCTOU race between IsValueCreated and .Value.
        System.Lazy<IniConfig> snapshot;
        _configLock.EnterReadLock();
        try { snapshot = _iniFile; }
        finally { _configLock.ExitReadLock(); }

        if (snapshot.IsValueCreated)
        {
            snapshot.Value.Flush();
        }
    }

    #endregion Public Methods

    #region Protected Methods

    /// <inheritdoc/>
    protected override void DisposeManaged()
    {
        if (_iniFile.IsValueCreated)
        {
            try { _iniFile.Value.Flush(); } catch { /* ignore */ }
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;

        _configFileWatcher?.Dispose();
        _configFileWatcher = null;

        _reloadGate.Dispose();
        _configLock.Dispose();
        _configContainerDict.Clear();
    }

    #endregion Protected Methods

    #region Private Methods

    private void SETUP_FILE_WATCHER()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;

        _configFileWatcher?.Dispose();
        _configFileWatcher = null;

        System.String currentPath;
        _configLock.EnterReadLock();
        try { currentPath = _configFilePath; }
        finally { _configLock.ExitReadLock(); }

        System.String? directory = System.IO.Path.GetDirectoryName(currentPath);
        System.String? file = System.IO.Path.GetFileName(currentPath);

        if (System.String.IsNullOrEmpty(directory) || System.String.IsNullOrEmpty(file))
        {
            return;
        }

        _configFileWatcher = new System.IO.FileSystemWatcher(directory, file)
        {
            NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size
        };

        // Capture path at setup time — lambda must not close over _configFilePath directly.
        System.String watchedPath = currentPath;

        _configFileWatcher.Changed += (_, e) =>
        {
            if (!e.FullPath.Equals(watchedPath, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Debounce: reset timer on every event so only the trailing edge triggers a reload.
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(
                _ => ReloadAll(),
                state: null,
                dueTime: _debounceDelay,
                period: System.Threading.Timeout.InfiniteTimeSpan);
        };

        _configFileWatcher.EnableRaisingEvents = true;
    }

    private System.Lazy<IniConfig> CREATE_LAZY_INI_CONFIG(System.String filePath)
    {
        return new System.Lazy<IniConfig>(() =>
        {
            ENSURE_CONFIG_DIRECTORY_EXISTS();
            return new IniConfig(filePath);
        }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private void VALIDATE_CONFIG_PATH(System.String pathToValidate)
    {
        var normalizedPath = System.IO.Path.GetFullPath(pathToValidate);
        var normalizedBaseDir = System.IO.Path.GetFullPath(_baseConfigDirectory);

        if (!normalizedBaseDir.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
        {
            normalizedBaseDir += System.IO.Path.DirectorySeparatorChar;
        }

        if (!normalizedPath.StartsWith(normalizedBaseDir, System.StringComparison.OrdinalIgnoreCase))
        {
            throw new InternalErrorException(
                $"Configuration file path '{pathToValidate}' is outside the allowed " +
                $"configuration directory '{_baseConfigDirectory}'.");
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ENSURE_CONFIG_DIRECTORY_EXISTS()
    {
        if (_directoryChecked) // volatile read — no lock needed
        {
            return;
        }

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

                if (!dirInfo.Exists)
                {
                    throw new System.InvalidOperationException($"Directory creation reported success but directory does not exist: {directory}");
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
            catch (System.Exception ex) when (ex is not System.InvalidOperationException)
            {
                throw new System.InvalidOperationException(
                    $"Unexpected error creating configuration directory: {directory}", ex);
            }
        }

        _directoryChecked = true; // volatile write
    }

    #endregion Private Methods
}