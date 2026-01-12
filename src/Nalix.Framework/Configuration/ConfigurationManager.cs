// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Environment;
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
/// This implementation includes thread safety, caching optimizations, and lazy loading.
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("ConfigFilePath = {ConfigFilePath,nq}, LoadedTypes = {_configContainerDict.Count}")]
public sealed class ConfigurationManager : SingletonBase<ConfigurationManager>
{
    #region Fields

    private readonly System.Lazy<IniConfig> _iniFile;
    private readonly System.Threading.ReaderWriterLockSlim _configLock;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, ConfigurationLoader> _configContainerDict;

    private System.Int32 _isReloading;
    private System.Boolean _directoryChecked;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public System.String ConfigFilePath { get; }

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
    private ConfigurationManager()
    {
        // Determine the configuration file path with validation
        System.String configDirectory = Directories.ConfigurationDirectory;
        
        // Validate the directory path for security
        if (System.String.IsNullOrWhiteSpace(configDirectory))
        {
            throw new System.InvalidOperationException("Configuration directory cannot be null or empty.");
        }

        // Get the full path to prevent path traversal attacks
        configDirectory = System.IO.Path.GetFullPath(configDirectory);

        this.ConfigFilePath = System.IO.Path.Combine(configDirectory, "configured.ini");

        // Validate the final configuration file path
        if (!this.ConfigFilePath.StartsWith(configDirectory, System.StringComparison.OrdinalIgnoreCase))
        {
            throw new System.Security.SecurityException(
                "Configuration file path is outside the allowed configuration directory.");
        }

        // Lazy-load the INI file to defer file access until needed
        _iniFile = new System.Lazy<IniConfig>(() =>
        {
            // Ensure the directory exists before trying to access the file
            this.ENSURE_CONFIG_DIRECTORY_EXISTS();
            return new IniConfig(ConfigFilePath);
        }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        _configContainerDict = new();
        _configLock = new(System.Threading.LockRecursionPolicy.NoRecursion);
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Initializes if needed and returns an instance of <typeparamref name="TClass"/>.
    /// </summary>
    /// <typeparam name="TClass">The type of the configuration container.</typeparam>
    /// <returns>An instance of type <typeparamref name="TClass"/>.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public TClass Get<TClass>() where TClass : ConfigurationLoader, new()
    {
        return (TClass)_configContainerDict.GetOrAdd(typeof(TClass), _ =>
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
    [System.Diagnostics.CodeAnalysis.MemberNotNull]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
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
    /// <returns>True if the configuration is loaded; otherwise, false.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Boolean IsLoaded<TClass>() where TClass : ConfigurationLoader => _configContainerDict.ContainsKey(typeof(TClass));

    /// <summary>
    /// Removes a specific configuration from the cache.
    /// </summary>
    /// <typeparam name="TClass">The configuration type to remove.</typeparam>
    /// <returns>True if the configuration was removed; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public System.Boolean Remove<TClass>() where TClass : ConfigurationLoader
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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

        // Clean up resources
        _configLock.Dispose();
        _configContainerDict.Clear();

        Dispose();
    }

    #endregion Protected Methods

    #region Private Methods

    /// <summary>
    /// Ensures the configuration directory exists with proper validation and error handling.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ENSURE_CONFIG_DIRECTORY_EXISTS()
    {
        if (!_directoryChecked)
        {
            System.String? directory = System.IO.Path.GetDirectoryName(ConfigFilePath);
            
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