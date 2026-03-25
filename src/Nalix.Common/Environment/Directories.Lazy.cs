// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    private static readonly System.Threading.ReaderWriterLockSlim DirectoryLock = new(System.Threading.LockRecursionPolicy.SupportsRecursion);

    /// <summary>
    /// Raised after a directory has been created. Handlers are isolated per-invocation.
    /// </summary>
    private static event System.Action<string> DirectoryCreated;

    // ---------- Configuration ----------

    /// <summary>Optional base path override (intended for tests).</summary>
    private static string _basePathOverride;

    /// <summary>
    /// Returns an environment variable value or <c>null</c> if empty/whitespace.
    /// </summary>
    /// <param name="name">Environment variable name.</param>
    /// <returns>Value or <c>null</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    private static string GET_ENV([System.Diagnostics.CodeAnalysis.DisallowNull] string name)
    {
        string value = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // ---------- Environment/Platform Detection ----------

    /// <summary>Whether the process is running inside a container (Docker/Kubernetes).</summary>
    private static readonly System.Lazy<bool> IsContainerLazy = new(() =>
    {
        try
        {
            string dotnetInContainer = System.Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            if (string.Equals(dotnetInContainer, "true", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string kubeHost = System.Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            if (!string.IsNullOrEmpty(kubeHost))
            {
                return true;
            }

            if (System.IO.File.Exists("/.dockerenv"))
            {
                return true;
            }

            if (System.IO.File.Exists("/proc/1/cgroup"))
            {
                string cg = System.IO.File.ReadAllText("/proc/1/cgroup");
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
    private static readonly System.Lazy<string> BasePathLazy = new(() =>
    {
        // 1) Explicit test override
        if (!string.IsNullOrEmpty(_basePathOverride))
        {
            return System.IO.Path.GetFullPath(_basePathOverride);
        }

        // 2) Environment override
        string env = GET_ENV("NALIX_BASE_PATH");
        if (!string.IsNullOrEmpty(env))
        {
            return System.IO.Path.GetFullPath(env);
        }

        // 3) Container defaults
        if (IsContainerLazy.Value)
        {
            return "/data";
        }

        // 4) Non-container, OS-aware
        if (System.OperatingSystem.IsWindows())
        {
            string root = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
            return System.IO.Path.Join(root, "Nalix");
        }
        // XDG base dir or ~/.local/share
        string xdg = GET_ENV("XDG_DATA_HOME");
        string dataHome = !string.IsNullOrEmpty(xdg)
            ? xdg
            : System.IO.Path.Join(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".local", "share");

        return System.IO.Path.Join(dataHome, "Nalix");
    });

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string RESOLVE_OR_ENV(
        [System.Diagnostics.CodeAnalysis.DisallowNull] string envName,
        [System.Diagnostics.CodeAnalysis.DisallowNull] string containerPath,
        [System.Diagnostics.CodeAnalysis.DisallowNull] string relative)
    {
        string env = GET_ENV(envName);
        return !string.IsNullOrEmpty(env)
            ? System.IO.Path.GetFullPath(env)
            : IsContainerLazy.Value && System.IO.Directory.Exists(containerPath)
            ? containerPath
            : System.IO.Path.Join(BasePathLazy.Value, relative);
    }

    // ---------- Lazies for each directory ----------

    private static readonly System.Lazy<string> DataPathLazy = new(() => ENSURE_AND_HARDEN(RESOLVE_OR_ENV("NALIX_DATA_PATH", "/data", "data")));

    private static readonly System.Lazy<string> LogsPathLazy = new(() => ENSURE_AND_HARDEN(RESOLVE_OR_ENV("NALIX_LOGS_PATH", "/logs", "logs"), UnixDirPerms.WorldReadable));

    private static readonly System.Lazy<string> ConfigPathLazy = new(() => ENSURE_AND_HARDEN(RESOLVE_OR_ENV("NALIX_CONFIG_PATH", "/config", "config"), UnixDirPerms.Private700));

    private static readonly System.Lazy<string> StoragePathLazy = new(() => ENSURE_AND_HARDEN(RESOLVE_OR_ENV("NALIX_STORAGE_PATH", "/storage", "storage"), UnixDirPerms.Shared750));

    private static readonly System.Lazy<string> DatabasePathLazy = new(() => ENSURE_AND_HARDEN(RESOLVE_OR_ENV("NALIX_DB_PATH", "/db", "db"), UnixDirPerms.Private700));

    private static readonly System.Lazy<string> CachesPathLazy = new(() => ENSURE_AND_HARDEN(System.IO.Path.Join(DataPathLazy.Value, "caches")));

    private static readonly System.Lazy<string> UploadsPathLazy = new(() => ENSURE_AND_HARDEN(System.IO.Path.Join(DataPathLazy.Value, "uploads"), UnixDirPerms.Shared750));

    private static readonly System.Lazy<string> BackupsPathLazy = new(() => ENSURE_AND_HARDEN(System.IO.Path.Join(DataPathLazy.Value, "backups"), UnixDirPerms.Private700));

    private static readonly System.Lazy<string> TempPathLazy = new(() =>
    {
        string path = RESOLVE_OR_ENV("NALIX_TEMP_PATH", "/tmp", System.IO.Path.Join("data", "tmp"));
        _ = ENSURE_AND_HARDEN(path, UnixDirPerms.Private700);

        int days = 7;
        string envDaysStr = GET_ENV("NALIX_TEMP_RETENTION_DAYS");
        if (!string.IsNullOrEmpty(envDaysStr) && int.TryParse(envDaysStr, out int envDaysParsed) && envDaysParsed > 0)
        {
            days = envDaysParsed;
        }

        _ = DeleteOldFiles(path, System.TimeSpan.FromDays(days));
        return path;
    });

    #endregion Private Properties
}