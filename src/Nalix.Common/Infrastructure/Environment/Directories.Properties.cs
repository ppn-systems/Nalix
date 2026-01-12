// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Environment;

/// <summary>
/// Provides application directories with environment-aware defaults and hardened creation.
/// </summary>
/// <remarks>
/// All members are thread-safe. Accessing any property ensures the directory exists
/// and applies OS-appropriate permissions. Not supported on browser targets.
/// </remarks>
public static partial class Directories
{
    /// <summary>
    /// Gets a value indicating whether the current process appears to be running inside a container.
    /// </summary>
    /// <remarks>
    /// Detection uses standard heuristics (e.g., <c>DOTNET_RUNNING_IN_CONTAINER</c>,
    /// <c>KUBERNETES_SERVICE_HOST</c>, presence of <c>/.dockerenv</c>, and <c>/proc/1/cgroup</c> markers).
    /// The value is computed once and cached.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.Boolean IsRunningInContainer
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => IsContainerLazy.Value;
    }

    /// <summary>
    /// Gets the resolved base directory for application assets.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item>Internal test override (when set).</item>
    ///   <item>Environment variable <c>NALIX_BASE_PATH</c>.</item>
    ///   <item>Container defaults (<c>/app/</c>, or <c>/data/</c>).</item>
    ///   <item>OS-specific default (Windows: <c>%ProgramData%\Nalix\</c>;
    ///       Unix: <c>$XDG_DATA_HOME</c> or <c>~/.local/share/Nalix/</c>).</item>
    /// </list>
    /// The returned path is absolute.
    /// </remarks>
    /// <value>A fully qualified directory path.</value>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String BaseAssetsDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => BasePathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for application logs.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><c>NALIX_LOGS_PATH</c> environment variable.</item>
    ///   <item>Container default: <c>/logs</c> (when present).</item>
    ///   <item><see cref="BaseAssetsDirectory"/>/<c>logs</c>.</item>
    /// </list>
    /// On Unix-like systems, permissions are set to be world-readable.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String LogsDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => LogsPathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for application data.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><c>NALIX_DATA_PATH</c> environment variable.</item>
    ///   <item>Container default: <c>/data</c> (when present).</item>
    ///   <item><see cref="BaseAssetsDirectory"/>/<c>data</c>.</item>
    /// </list>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String DataDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => DataPathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for configuration files.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><c>NALIX_CONFIG_PATH</c> environment variable.</item>
    ///   <item>Container default: <c>/config</c> (when present).</item>
    ///   <item><see cref="DataDirectory"/>/<c>config</c>.</item>
    /// </list>
    /// Directory permissions are hardened (Unix: 0700).
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String ConfigurationDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => ConfigPathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for temporary files.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><c>NALIX_TEMP_PATH</c> environment variable.</item>
    ///   <item>Container default: <c>/tmp</c> (when present).</item>
    ///   <item><see cref="DataDirectory"/>/<c>tmp</c>.</item>
    /// </list>
    /// The directory is created with restricted permissions (Unix: 0700). On first access,
    /// files older than a retention period are removed. The retention period defaults to
    /// 7 days and can be overridden via <c>NALIX_TEMP_RETENTION_DAYS</c> (positive integer).
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String TemporaryDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => TempPathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for persistent storage.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><c>NALIX_STORAGE_PATH</c> environment variable.</item>
    ///   <item>Container default: <c>/storage</c> (when present).</item>
    ///   <item><see cref="DataDirectory"/>/<c>storage</c>.</item>
    /// </list>
    /// Directory permissions are set for shared read/execute on Unix (0750).
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String StorageDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => StoragePathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for database files.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><c>NALIX_DB_PATH</c> environment variable.</item>
    ///   <item>Container default: <c>/db</c> (when present).</item>
    ///   <item><see cref="DataDirectory"/>/<c>db</c>.</item>
    /// </list>
    /// Directory permissions are restricted (Unix: 0700).
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String DatabaseDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => DatabasePathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for cache files.
    /// </summary>
    /// <remarks>
    /// Default location: <see cref="DataDirectory"/>/<c>caches</c>. The directory is created on first access.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String CacheDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => CachesPathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for uploaded files.
    /// </summary>
    /// <remarks>
    /// Default location: <see cref="DataDirectory"/>/<c>uploads</c>. Directory permissions on Unix are set to 0750.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String UploadsDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => UploadsPathLazy.Value;
    }

    /// <summary>
    /// Gets the directory used for backups.
    /// </summary>
    /// <remarks>
    /// Default location: <see cref="DataDirectory"/>/<c>backups</c>. Directory permissions on Unix are restricted (0700).
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.NotNull]
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)]
    public static System.String BackupsDirectory
    {
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        get => BackupsPathLazy.Value;
    }
}
