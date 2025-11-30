// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using Nalix.Common.Diagnostics;
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
/// - A <see cref="SemaphoreSlim"/>(1,1) gate serialises reload/path-change operations
///   and makes callers WAIT instead of silently returning <see langword="false"/>.
/// - <see cref="FileSystemWatcher"/> changes are debounced (300 ms) to absorb the
///   multiple rapid events that most OS file-writers emit for a single logical write.
/// </para>
/// </remarks>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("ConfigFilePath = {ConfigFilePath,nq}, LoadedTypes = {_configContainerDict.Count}")]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.NonPublicMethods |
    DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class ConfigurationManager : SingletonBase<ConfigurationManager>
{
    #region Fields

    /// <summary>
    /// volatile: assigned atomically in SetConfigFilePath; all threads must see the latest reference.
    /// </summary>
    private volatile Lazy<IniConfig> _iniFile;

    private readonly string _baseConfigDirectory;
    private readonly ReaderWriterLockSlim _configLock;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Lazy<ConfigurationLoader>> _configContainerDict;

    /// <summary>
    /// SemaphoreSlim(1,1): serialises ReloadAll / SetConfigFilePath.
    /// Using Wait() instead of Interlocked.Exchange means callers BLOCK until the current
    /// operation completes — fixing the test failure where FileSystemWatcher would grab the
    /// old Interlocked flag just before the test's manual ReloadAll() call.
    /// </summary>
    private readonly SemaphoreSlim _reloadGate;

    /// <summary>
    /// Debounce: FileSystemWatcher fires 2-3 Changed events per single file write on most OSes.
    /// We reset a one-shot timer on every event; only the last one fires ReloadAll().
    /// </summary>
    private Timer? _debounceTimer;

    private static readonly TimeSpan s_debounceDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// volatile: read/written from multiple threads without a full lock.
    /// </summary>
    private volatile bool _directoryChecked;

    private string _configFilePath;
    private FileSystemWatcher? _configFileWatcher;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the path to the active configuration file.
    /// </summary>
    public string ConfigFilePath
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
    public bool ConfigFileExists
    {
        get
        {
            // Snapshot under read lock to avoid a TOCTOU race with SetConfigFilePath.
            Lazy<IniConfig> snapshot;
            _configLock.EnterReadLock();
            try { snapshot = _iniFile; }
            finally { _configLock.ExitReadLock(); }

            return snapshot.IsValueCreated && snapshot.Value.ExistsFile;
        }
    }

    /// <summary>
    /// Gets the last reload timestamp.
    /// </summary>
    public DateTime LastReloadTime { get; private set; } = DateTime.UtcNow;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationManager"/> class.
    /// </summary>
    public ConfigurationManager()
    {
        string configDirectory = Directories.ConfigurationDirectory;

        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            throw new InvalidOperationException("Configuration directory cannot be null or empty.");
        }

        _baseConfigDirectory = Path.GetFullPath(configDirectory);
        _configFilePath = Path.Combine(_baseConfigDirectory, "default.ini");

        this.VALIDATE_CONFIG_PATH(_configFilePath);

        // Initialise synchronisation primitives BEFORE the watcher.
        // If the watcher fires during construction it needs _configLock and _reloadGate to exist.
        _configLock = new(LockRecursionPolicy.NoRecursion);
        _reloadGate = new SemaphoreSlim(1, 1);
        _configContainerDict = new();

        _iniFile = this.CREATE_LAZY_INI_CONFIG(_configFilePath);
        this.SETUP_FILE_WATCHER();
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
    /// <exception cref="ArgumentException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool SetConfigFilePath(string newConfigFilePath, bool autoReload = true)
    {
        if (string.IsNullOrWhiteSpace(newConfigFilePath))
        {
            throw new ArgumentException(
                "Configuration file path cannot be null or whitespace.",
                nameof(newConfigFilePath));
        }

        string normalizedPath = Path.GetFullPath(newConfigFilePath);
        this.VALIDATE_CONFIG_PATH(normalizedPath);

        // Wait up to 5 s for any concurrent reload/path-change to finish.
        if (!_reloadGate.Wait(TimeSpan.FromSeconds(5)))
        {
            return false;
        }

        bool success = false;
        string? pathToWatch = null;

        try
        {
            _configLock.EnterWriteLock();
            try
            {
                if (string.Equals(_configFilePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // no-op
                }

                if (_iniFile.IsValueCreated)
                {
                    try { _iniFile.Value.Flush(); } catch { /* ignore flush errors on the old file */ }
                }

                string oldPath = _configFilePath;
                _configFilePath = normalizedPath;
                _directoryChecked = false;
                _iniFile = this.CREATE_LAZY_INI_CONFIG(_configFilePath);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                    .Info($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                          $"path-changed from='{oldPath}' to='{normalizedPath}'");

                if (autoReload && !_configContainerDict.IsEmpty)
                {
                    try
                    {
                        IniConfig newIniFile = _iniFile.Value; // force-load the new file

                        foreach (Lazy<ConfigurationLoader> lazy in _configContainerDict.Values)
                        {
                            if (lazy.IsValueCreated)
                            {
                                lazy.Value.Initialize(newIniFile);
                            }
                        }

                        this.LastReloadTime = DateTime.UtcNow;

                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Info($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                                                      $"auto-reload-ok count={_configContainerDict.Count}");

                        pathToWatch = normalizedPath;
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                            .Error($"[FW.{nameof(ConfigurationManager)}:{nameof(SetConfigFilePath)}] " +
                                   $"auto-reload-fail msg={ex.Message}", ex);

                        // Roll back state — do NOT call SETUP_FILE_WATCHER inside the write lock.
                        _configFilePath = oldPath;
                        _directoryChecked = false;
                        _iniFile = this.CREATE_LAZY_INI_CONFIG(oldPath);

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
                this.SETUP_FILE_WATCHER();
            }

            return success;
        }
        finally
        {
            _ = _reloadGate.Release();
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
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TClass Get<TClass>() where TClass : ConfigurationLoader, new()
    {
        Lazy<IniConfig> iniSnapshot;
        _configLock.EnterReadLock();

        try
        {
            iniSnapshot = _iniFile;
        }
        finally
        {
            _configLock.ExitReadLock();
        }

        Lazy<ConfigurationLoader> lazy = _configContainerDict.GetOrAdd(
            typeof(TClass),
            _ => new Lazy<ConfigurationLoader>(() =>
            {
                TClass container = new();
                container.Initialize(iniSnapshot.Value);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[FW.{nameof(ConfigurationManager)}:{nameof(Get)}] create {typeof(TClass).Name}");

                return container;
            }, LazyThreadSafetyMode.ExecutionAndPublication)
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
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="configFilePath"/> is null or whitespace.
    /// </exception>
    /// <exception cref="SecurityException">
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
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TClass Get<TClass>(string configFilePath, bool autoReload = true)
        where TClass : ConfigurationLoader, new()
    {
        _ = this.SetConfigFilePath(configFilePath, autoReload);
        return this.Get<TClass>();
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
    /// <see cref="FileSystemWatcher"/> can start a background reload right before
    /// the test calls <see cref="ReloadAll"/> manually; the old <c>Interlocked</c> flag would
    /// cause the manual call to return <see langword="false"/> immediately.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ReloadAll()
    {
        if (!_reloadGate.Wait(TimeSpan.FromSeconds(5)))
        {
            return false;
        }

        bool reloadSuccess = false;
        Exception? reloadException = null;

        try
        {
            _configLock.EnterWriteLock();
            try
            {
                if (_iniFile.IsValueCreated)
                {
                    _iniFile.Value.Reload();
                }

                foreach (Lazy<ConfigurationLoader> lazy in _configContainerDict.Values)
                {
                    if (lazy.IsValueCreated)
                    {
                        lazy.Value.Initialize(_iniFile.Value);
                    }
                }

                this.LastReloadTime = DateTime.UtcNow;
                reloadSuccess = true;
            }
            catch (Exception ex)
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
                           $"reload-fail msg={reloadException?.Message}", reloadException!);
            }

            return reloadSuccess;
        }
        finally
        {
            _ = _reloadGate.Release();
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
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsLoaded<TClass>() where TClass : ConfigurationLoader => _configContainerDict.ContainsKey(typeof(TClass));

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
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool Remove<TClass>() where TClass : ConfigurationLoader
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[FW.{nameof(ConfigurationManager)}:{nameof(Remove)}] remove {typeof(TClass).Name}");

        return _configContainerDict.TryRemove(typeof(TClass), out _);
    }

    /// <summary>
    /// Clears all cached configurations.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe. All configurations will be reinitialized on
    /// the next call to <see cref="Get{TClass}()"/> for each type.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ClearAll()
    {
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[FW.{nameof(ConfigurationManager)}:{nameof(ClearAll)}] clear-all");

        _configContainerDict.Clear();
    }

    /// <summary>
    /// Ensures that changes are written to disk.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and will flush pending changes to the INI file.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Flush()
    {
        // Snapshot under read lock — avoids TOCTOU race between IsValueCreated and .Value.
        Lazy<IniConfig> snapshot;
        _configLock.EnterReadLock();
        try { snapshot = _iniFile; }
        finally { _configLock.ExitReadLock(); }

        if (snapshot.IsValueCreated)
        {
            try
            {
                snapshot.Value.Flush();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[FW.{nameof(ConfigurationManager)}:{nameof(Flush)}] flushed");
            }
            catch (Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[FW.{nameof(ConfigurationManager)}:{nameof(Flush)}] flushErr msg={ex.Message}", ex);
                throw;
            }
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

        string currentPath;
        _configLock.EnterReadLock();
        try { currentPath = _configFilePath; }
        finally { _configLock.ExitReadLock(); }

        string? directory = Path.GetDirectoryName(currentPath);
        string? file = Path.GetFileName(currentPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(file))
        {
            return;
        }

        _configFileWatcher = new FileSystemWatcher(directory, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        // Capture path at setup time — lambda must not close over _configFilePath directly.
        string watchedPath = currentPath;

        _configFileWatcher.Changed += (_, e) =>
        {
            if (!e.FullPath.Equals(watchedPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Debounce: reset timer on every event so only the trailing edge triggers a reload.
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ =>
                {
                    if (!this.ReloadAll())
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[FW.{nameof(ConfigurationManager)}:Watcher] reload-failed path={watchedPath}");
                    }
                },
                state: null,
                dueTime: s_debounceDelay,
                period: Timeout.InfiniteTimeSpan);
        };

        _configFileWatcher.EnableRaisingEvents = true;
    }

    private Lazy<IniConfig> CREATE_LAZY_INI_CONFIG(string filePath)
    {
        return new Lazy<IniConfig>(() =>
        {
            this.ENSURE_CONFIG_DIRECTORY_EXISTS();
            return new IniConfig(filePath);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private void VALIDATE_CONFIG_PATH(string pathToValidate)
    {
        string normalizedPath = Path.GetFullPath(pathToValidate);
        string normalizedBaseDir = Path.GetFullPath(_baseConfigDirectory);

        if (!normalizedBaseDir.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture))
        {
            normalizedBaseDir += Path.DirectorySeparatorChar;
        }

        if (!normalizedPath.StartsWith(normalizedBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new InternalErrorException(
                $"Configuration file path '{pathToValidate}' is outside the allowed " +
                $"configuration directory '{_baseConfigDirectory}'.");
        }
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ENSURE_CONFIG_DIRECTORY_EXISTS()
    {
        if (_directoryChecked) // volatile read — no lock needed
        {
            return;
        }

        string? directory = Path.GetDirectoryName(_configFilePath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "Configuration file path does not contain a valid directory component.");
        }

        if (!Directory.Exists(directory))
        {
            try
            {
                DirectoryInfo dirInfo = Directory.CreateDirectory(directory);

                if (!dirInfo.Exists)
                {
                    throw new InvalidOperationException($"Directory creation reported success but directory does not exist: {directory}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException(
                    $"Access denied when creating configuration directory: {directory}", ex);
            }
            catch (PathTooLongException ex)
            {
                throw new PathTooLongException(
                    $"Configuration directory path is too long: {directory}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException(
                    $"I/O error creating configuration directory: {directory}", ex);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Unexpected error creating configuration directory: {directory}", ex);
            }
        }

        _directoryChecked = true; // volatile write
    }

    #endregion Private Methods
}
