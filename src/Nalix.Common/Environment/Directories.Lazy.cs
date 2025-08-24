// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Environment;

/// <summary>
/// Provides application-wide directories with environment-aware defaults,
/// safe creation, and optional environment variable overrides.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
public static partial class Directories
{
    #region Private Properties

    // ---------- Locks & Events ----------

    /// <summary>Global lock for thread-safe directory creation.</summary>
    private static readonly System.Threading.ReaderWriterLockSlim DirectoryLock =
        new(System.Threading.LockRecursionPolicy.SupportsRecursion);

    /// <summary>
    /// Raised after a directory has been created. Handlers are isolated per-invocation.
    /// </summary>
    private static event System.Action<System.String> DirectoryCreated;

    // ---------- Configuration ----------

    /// <summary>Optional base path override (intended for tests).</summary>
    private static System.String _basePathOverride;

    /// <summary>
    /// Returns an environment variable value or <c>null</c> if empty/whitespace.
    /// </summary>
    /// <param name="name">Environment variable name.</param>
    /// <returns>Value or <c>null</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    private static System.String GetEnv([System.Diagnostics.CodeAnalysis.DisallowNull] System.String name)
    {
        System.String value = System.Environment.GetEnvironmentVariable(name);
        return System.String.IsNullOrWhiteSpace(value) ? null : value;
    }

    // ---------- Environment/Platform Detection ----------

    /// <summary>Whether the process is running inside a container (Docker/Kubernetes).</summary>
    private static readonly System.Lazy<System.Boolean> IsContainerLazy = new(() =>
    {
        try
        {
            System.String dotnetInContainer = System.Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            if (System.String.Equals(dotnetInContainer, "true", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            System.String kubeHost = System.Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            if (!System.String.IsNullOrEmpty(kubeHost))
            {
                return true;
            }

            if (System.IO.File.Exists("/.dockerenv"))
            {
                return true;
            }

            if (System.IO.File.Exists("/proc/1/cgroup"))
            {
                System.String cg = System.IO.File.ReadAllText("/proc/1/cgroup");
                if (cg.Contains("docker", System.StringComparison.OrdinalIgnoreCase) ||
                    cg.Contains("kubepods", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    });

    // ---------- Path Resolution ----------

    /// <summary>Base path resolution with ENV and platform-aware fallbacks.</summary>
    private static readonly System.Lazy<System.String> BasePathLazy = new(() =>
    {
        // 1) Explicit test override
        if (!System.String.IsNullOrEmpty(_basePathOverride))
        {
            return System.IO.Path.GetFullPath(_basePathOverride);
        }

        // 2) Environment override
        System.String env = GetEnv("NALIX_BASE_PATH");
        if (!System.String.IsNullOrEmpty(env))
        {
            return System.IO.Path.GetFullPath(env);
        }

        // 3) Container defaults
        if (IsContainerLazy.Value)
        {
            return System.IO.Directory.Exists("/assets") ? "/assets" : System.IO.Directory.Exists("/app") ? "/app/assets" : "/data/assets";
        }

        // 4) Non-container, OS-aware
        if (System.OperatingSystem.IsWindows())
        {
            System.String root = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
            return System.IO.Path.Join(root, "Nalix", "assets");
        }
        else
        {
            // XDG base dir or ~/.local/share
            System.String xdg = GetEnv("XDG_DATA_HOME");
            System.String dataHome = !System.String.IsNullOrEmpty(xdg)
                ? xdg
                : System.IO.Path.Join(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".local", "share");
            return System.IO.Path.Join(dataHome, "Nalix", "assets");
        }
    });

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String ResolveOrEnv(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String envName,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String containerPath,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.String relative)
    {
        System.String env = GetEnv(envName);
        return !System.String.IsNullOrEmpty(env)
            ? System.IO.Path.GetFullPath(env)
            : IsContainerLazy.Value && System.IO.Directory.Exists(containerPath)
            ? containerPath
            : System.IO.Path.Join(BasePathLazy.Value, relative);
    }

    // ---------- Lazies for each directory ----------

    private static readonly System.Lazy<System.String> DataPathLazy = new(() =>
        EnsureAndHarden(ResolveOrEnv("NALIX_DATA_PATH", "/data", "data")));

    private static readonly System.Lazy<System.String> LogsPathLazy = new(() =>
        EnsureAndHarden(ResolveOrEnv("NALIX_LOGS_PATH", "/logs", System.IO.Path.Join("data", "logs")), UnixDirPerms.WorldReadable));

    private static readonly System.Lazy<System.String> ConfigPathLazy = new(() =>
        EnsureAndHarden(ResolveOrEnv("NALIX_CONFIG_PATH", "/config", System.IO.Path.Join("data", "config")), UnixDirPerms.Private700));

    private static readonly System.Lazy<System.String> StoragePathLazy = new(() =>
        EnsureAndHarden(ResolveOrEnv("NALIX_STORAGE_PATH", "/storage", System.IO.Path.Join("data", "storage")), UnixDirPerms.Shared750));

    private static readonly System.Lazy<System.String> DatabasePathLazy = new(() =>
        EnsureAndHarden(ResolveOrEnv("NALIX_DB_PATH", "/db", System.IO.Path.Join("data", "db")), UnixDirPerms.Private700));

    private static readonly System.Lazy<System.String> CachesPathLazy = new(() =>
        EnsureAndHarden(System.IO.Path.Join(DataPathLazy.Value, "caches")));

    private static readonly System.Lazy<System.String> UploadsPathLazy = new(() =>
        EnsureAndHarden(System.IO.Path.Join(DataPathLazy.Value, "uploads"), UnixDirPerms.Shared750));

    private static readonly System.Lazy<System.String> BackupsPathLazy = new(() =>
        EnsureAndHarden(System.IO.Path.Join(DataPathLazy.Value, "backups"), UnixDirPerms.Private700));

    private static readonly System.Lazy<System.String> TempPathLazy = new(() =>
    {
        System.String path = ResolveOrEnv("NALIX_TEMP_PATH", "/tmp", System.IO.Path.Join("data", "tmp"));
        _ = EnsureAndHarden(path, UnixDirPerms.Private700);

        System.Int32 days = 7;
        System.String envDaysStr = GetEnv("NALIX_TEMP_RETENTION_DAYS");
        if (!System.String.IsNullOrEmpty(envDaysStr))
        {
            if (System.Int32.TryParse(envDaysStr, out System.Int32 envDaysParsed) && envDaysParsed > 0)
            {
                days = envDaysParsed;
            }
        }

        _ = CleanupDirectory(path, System.TimeSpan.FromDays(days));
        return path;
    });

    #endregion Private Properties
}
