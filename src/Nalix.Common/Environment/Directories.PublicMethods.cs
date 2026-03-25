// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Environment;

public static partial class Directories
{
    // -------- Events registration --------

    /// <summary>
    /// Registers a directory creation event handler.
    /// </summary>
    /// <param name="handler">
    /// The handler to invoke when a directory is created.
    /// </param>
    public static void RegisterDirectoryCreationHandler(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Action<string> handler)
    {
        System.ArgumentNullException.ThrowIfNull(handler);
        DirectoryCreated += handler;
    }

    /// <summary>
    /// Unregisters a directory creation event handler.
    /// </summary>
    /// <param name="handler">
    /// The handler to remove.
    /// </param>
    public static void UnregisterDirectoryCreationHandler(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Action<string> handler)
    {
        System.ArgumentNullException.ThrowIfNull(handler);
        DirectoryCreated -= handler;
    }

    // -------- Helpers to build paths --------

    /// <summary>
    /// Creates a subdirectory under the specified parent directory, if it does not exist.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string CreateSubdirectory(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string parentPath,
        [System.Diagnostics.CodeAnalysis.MaybeNull] string directoryName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        if (string.IsNullOrWhiteSpace(directoryName))
        {
            throw new System.ArgumentNullException(nameof(directoryName));
        }

        string fullPath = COMBINE_SAFE(parentPath, directoryName);
        ENSURE_DIRECTORY_EXISTS(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates a subdirectory with a UTC timestamp-based name (yyyyMMddTHHmmssZ).
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string CreateTimestampedDirectory(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string parentPath,
        [System.Diagnostics.CodeAnalysis.NotNull] string prefix = "")
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        string timestamp = System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        string directoryName = string.IsNullOrEmpty(prefix) ? timestamp : (prefix + "_" + timestamp);

        return CreateSubdirectory(parentPath, directoryName);
    }

    /// <summary>
    /// Returns a full file path under a given directory, ensuring the directory exists.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string GetFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string directoryPath,
        [System.Diagnostics.CodeAnalysis.MaybeNull] string fileName)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new System.ArgumentNullException(nameof(fileName));
        }

        ENSURE_DIRECTORY_EXISTS(directoryPath);
        return COMBINE_SAFE(directoryPath, fileName);
    }

    /// <summary>
    /// Returns a temp file path under <see cref="TemporaryDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string GetTempFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string fileName) => GetFilePath(TemporaryDirectory, fileName);

    /// <summary>
    /// Returns a timestamped file path (yyyyMMddTHHmmssZ) under a directory.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string GetTimestampedFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string directoryPath,
        [System.Diagnostics.CodeAnalysis.MaybeNull] string fileNameBase,
        [System.Diagnostics.CodeAnalysis.MaybeNull] string extension)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (string.IsNullOrWhiteSpace(fileNameBase))
        {
            throw new System.ArgumentNullException(nameof(fileNameBase));
        }

        string timestamp = System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        string fileName = fileNameBase + "_" + timestamp + "." + extension.TrimStart('.');

        return GetFilePath(directoryPath, fileName);
    }

    /// <summary>
    /// Returns a log file path under <see cref="LogsDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string GetLogFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string fileName) => GetFilePath(LogsDirectory, fileName);

    /// <summary>
    /// Returns a config file path under <see cref="ConfigurationDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string GetConfigFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string fileName) => GetFilePath(ConfigurationDirectory, fileName);

    /// <summary>
    /// Returns a storage file path under <see cref="StorageDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string GetStorageFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string fileName) => GetFilePath(StorageDirectory, fileName);

    /// <summary>
    /// Returns a database file path under <see cref="DatabaseDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string GetDatabaseFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string fileName) => GetFilePath(DatabaseDirectory, fileName);

    // -------- Maintenance --------

    /// <summary>Deletes files older than the specified age in a directory.</summary>
    /// <param name="directoryPath">The directory to clean.</param>
    /// <param name="maxAge">The maximum age to keep.</param>
    /// <param name="searchPattern">Glob pattern to select files.</param>
    /// <returns>Number of files deleted.</returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static int DeleteOldFiles(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string directoryPath,
        [System.Diagnostics.CodeAnalysis.NotNull] System.TimeSpan maxAge,
        [System.Diagnostics.CodeAnalysis.NotNull] string searchPattern = "*")
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (!System.IO.Directory.Exists(directoryPath))
        {
            return 0;
        }

        int deleted = 0;
        System.DateTime cutoff = System.DateTime.UtcNow - maxAge;

        System.IO.EnumerationOptions opts = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true
        };

        try
        {
            foreach (string filePath in System.IO.Directory.EnumerateFiles(directoryPath, searchPattern, opts))
            {
                try
                {
                    System.IO.FileInfo fi = new(filePath);
                    if (fi.LastWriteTimeUtc < cutoff)
                    {
                        fi.Delete();
                        deleted++;
                    }
                }
                catch { }
            }
        }
        catch { }

        return deleted;
    }

    /// <summary>
    /// Validates that all known directories are accessible by writing a temporary file.
    /// </summary>
    /// <returns>
    /// <c>true</c> if all directories accept writes; otherwise <c>false</c>.
    /// </returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static bool CanAccessAllDirectories()
    {
        try
        {
            string[] testPaths =
            [
                LogsDirectory,
                DataDirectory,
                CacheDirectory,
                UploadsDirectory,
                BackupsDirectory,
                StorageDirectory,
                DatabaseDirectory,
                TemporaryDirectory,
                ConfigurationDirectory
            ];

            for (int i = 0; i < testPaths.Length; i++)
            {
                string path = testPaths[i];
                string test = System.IO.Path.Join(path, "test_" + System.Guid.NewGuid().ToString("N") + ".tmp");
                using (System.IO.File.Create(test)) { }
                System.IO.File.Delete(test);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Overrides the base path for testing. The override is not persisted across process restarts.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    public static void SetBasePathOverride(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new System.ArgumentNullException(nameof(path));
        }

        _basePathOverride = path;
    }

    /// <summary>
    /// Enumerates files from a directory with optional recursion.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Collections.Generic.IEnumerable<string> EnumerateFiles(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string directory,
        [System.Diagnostics.CodeAnalysis.NotNull] string searchPattern = "*",
        [System.Diagnostics.CodeAnalysis.NotNull] bool recursive = false)
    {
        return string.IsNullOrWhiteSpace(directory)
            ? throw new System.ArgumentNullException(nameof(directory))
            : EnumerateFilesCore();
        System.Collections.Generic.IEnumerable<string> EnumerateFilesCore()
        {
            if (!System.IO.Directory.Exists(directory))
            {
                yield break;
            }

            System.IO.EnumerationOptions opts = new()
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true
            };

            foreach (string file in System.IO.Directory.EnumerateFiles(directory, searchPattern, opts))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Computes the size of a directory in bytes.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static long CalculateDirectorySize(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string directoryPath,
        [System.Diagnostics.CodeAnalysis.NotNull] bool includeSubdirectories = true)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (!System.IO.Directory.Exists(directoryPath))
        {
            return 0;
        }

        long size = 0;

        try
        {
            System.IO.EnumerationOptions opts = new()
            {
                RecurseSubdirectories = includeSubdirectories,
                IgnoreInaccessible = true
            };

            foreach (string file in System.IO.Directory.EnumerateFiles(directoryPath, "*", opts))
            {
                try { size += new System.IO.FileInfo(file).Length; } catch { }
            }
        }
        catch { }

        return size;
    }

    /// <summary>
    /// Creates a date-based directory (yyyy-MM-dd) under the specified parent path.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string CreateDateDirectory(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        string dir = System.IO.Path.Join(parentPath, System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
        ENSURE_DIRECTORY_EXISTS(dir);
        return dir;
    }

    /// <summary>
    /// Creates a hierarchical date-based directory (yyyy/MM/dd) under the specified parent path.
    /// </summary>
    /// <exception cref="System.ArgumentNullException"></exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string CreateHierarchicalDateDirectory(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        System.DateTime now = System.DateTime.UtcNow;
        string year = System.IO.Path.Join(parentPath, now.ToString("yyyy"));
        string month = System.IO.Path.Join(year, now.ToString("MM"));
        string day = System.IO.Path.Join(month, now.ToString("dd"));

        ENSURE_DIRECTORY_EXISTS(day);
        return day;
    }

    /// <summary>
    /// Returns a sharded file path under a parent directory to reduce directory fan-out.
    /// </summary>
    /// <param name="parentPath">The parent path.</param>
    /// <param name="key">A key used to derive shard segments.</param>
    /// <param name="depth">Number of shard levels (e.g., 2).</param>
    /// <param name="width">Hex digits per level (e.g., 2 => 00..FF).</param>
    /// <exception cref="System.ArgumentNullException"></exception>
    /// <exception cref="System.ArgumentOutOfRangeException"></exception>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static string EnsureShardedPath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] string parentPath,
        [System.Diagnostics.CodeAnalysis.MaybeNull] string key,
        [System.Diagnostics.CodeAnalysis.NotNull] int depth = 2,
        [System.Diagnostics.CodeAnalysis.NotNull] int width = 2)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        if (depth < 1 || width < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(depth));
        }

        // FNV-1a 32-bit
        unchecked
        {
            uint h = 2166136261;
            for (int i = 0; i < key.Length; i++)
            {
                h ^= key[i];
                h *= 16777619;
            }

            string p = parentPath;
            for (int i = 0; i < depth; i++)
            {
                uint mask = (uint)((1 << (width * 4)) - 1);
                string seg = (h & mask).ToString("X" + width.ToString());
                p = System.IO.Path.Join(p, seg);
                h >>= width * 4;
            }
            ENSURE_DIRECTORY_EXISTS(p);
            return System.IO.Path.Join(p, key);
        }
    }
}