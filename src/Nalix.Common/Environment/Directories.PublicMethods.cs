// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

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
        [MaybeNull] Action<string> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        DirectoryCreated += handler;
    }

    /// <summary>
    /// Unregisters a directory creation event handler.
    /// </summary>
    /// <param name="handler">
    /// The handler to remove.
    /// </param>
    public static void UnregisterDirectoryCreationHandler(
        [MaybeNull] Action<string> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        DirectoryCreated -= handler;
    }

    // -------- Helpers to build paths --------

    /// <summary>
    /// Creates a subdirectory under the specified parent directory, if it does not exist.
    /// </summary>
    /// <param name="parentPath">
    /// The parent directory where the subdirectory should be created.
    /// </param>
    /// <param name="directoryName">
    /// The name of the subdirectory to create.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    [return: NotNull]
    public static string CreateSubdirectory(
        [MaybeNull] string parentPath,
        [MaybeNull] string directoryName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentNullException(nameof(parentPath));
        }

        if (string.IsNullOrWhiteSpace(directoryName))
        {
            throw new ArgumentNullException(nameof(directoryName));
        }

        string fullPath = COMBINE_SAFE(parentPath, directoryName);
        ENSURE_DIRECTORY_EXISTS(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates a subdirectory with a UTC timestamp-based name (yyyyMMddTHHmmssZ).
    /// </summary>
    /// <param name="parentPath">
    /// The parent directory where the timestamped directory should be created.
    /// </param>
    /// <param name="prefix">
    /// An optional prefix to prepend to the generated timestamp-based directory name.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    [return: NotNull]
    public static string CreateTimestampedDirectory(
        [MaybeNull] string parentPath,
        [NotNull] string prefix = "")
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentNullException(nameof(parentPath));
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        string directoryName = string.IsNullOrEmpty(prefix) ? timestamp : (prefix + "_" + timestamp);

        return CreateSubdirectory(parentPath, directoryName);
    }

    /// <summary>
    /// Returns a full file path under a given directory, ensuring the directory exists.
    /// </summary>
    /// <param name="directoryPath">
    /// The directory that will contain the file.
    /// </param>
    /// <param name="fileName">
    /// The file name to append to the directory path.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    [return: NotNull]
    public static string GetFilePath(
        [MaybeNull] string directoryPath,
        [MaybeNull] string fileName)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentNullException(nameof(directoryPath));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        ENSURE_DIRECTORY_EXISTS(directoryPath);
        return COMBINE_SAFE(directoryPath, fileName);
    }

    /// <summary>
    /// Returns a temp file path under <see cref="TemporaryDirectory"/>.
    /// </summary>
    /// <param name="fileName">
    /// The file name to place under the temporary directory.
    /// </param>
    [return: NotNull]
    public static string GetTempFilePath(
        [MaybeNull] string fileName) => GetFilePath(TemporaryDirectory, fileName);

    /// <summary>
    /// Returns a timestamped file path (yyyyMMddTHHmmssZ) under a directory.
    /// </summary>
    /// <param name="directoryPath">
    /// The directory that will contain the generated file.
    /// </param>
    /// <param name="fileNameBase">
    /// The base file name to use before the timestamp suffix.
    /// </param>
    /// <param name="extension">
    /// The file extension to append to the generated file name.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    [return: NotNull]
    public static string GetTimestampedFilePath(
        [MaybeNull] string directoryPath,
        [MaybeNull] string fileNameBase,
        [MaybeNull] string extension)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentNullException(nameof(directoryPath));
        }

        if (string.IsNullOrWhiteSpace(fileNameBase))
        {
            throw new ArgumentNullException(nameof(fileNameBase));
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        string fileName = fileNameBase + "_" + timestamp + "." + extension.TrimStart('.');

        return GetFilePath(directoryPath, fileName);
    }

    /// <summary>
    /// Returns a log file path under <see cref="LogsDirectory"/>.
    /// </summary>
    /// <param name="fileName">
    /// The file name to place under the logs directory.
    /// </param>
    [return: NotNull]
    public static string GetLogFilePath(
        [MaybeNull] string fileName) => GetFilePath(LogsDirectory, fileName);

    /// <summary>
    /// Returns a config file path under <see cref="ConfigurationDirectory"/>.
    /// </summary>
    /// <param name="fileName">
    /// The file name to place under the configuration directory.
    /// </param>
    [return: NotNull]
    public static string GetConfigFilePath(
        [MaybeNull] string fileName) => GetFilePath(ConfigurationDirectory, fileName);

    /// <summary>
    /// Returns a storage file path under <see cref="StorageDirectory"/>.
    /// </summary>
    /// <param name="fileName">
    /// The file name to place under the storage directory.
    /// </param>
    [return: NotNull]
    public static string GetStorageFilePath(
        [MaybeNull] string fileName) => GetFilePath(StorageDirectory, fileName);

    /// <summary>
    /// Returns a database file path under <see cref="DatabaseDirectory"/>.
    /// </summary>
    /// <param name="fileName">
    /// The file name to place under the database directory.
    /// </param>
    [return: NotNull]
    public static string GetDatabaseFilePath(
        [MaybeNull] string fileName) => GetFilePath(DatabaseDirectory, fileName);

    // -------- Maintenance --------

    /// <summary>Deletes files older than the specified age in a directory.</summary>
    /// <param name="directoryPath">The directory to clean.</param>
    /// <param name="maxAge">The maximum age to keep.</param>
    /// <param name="searchPattern">Glob pattern to select files.</param>
    /// <returns>Number of files deleted.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    [return: NotNull]
    public static int DeleteOldFiles(
        [MaybeNull] string directoryPath,
        [NotNull] TimeSpan maxAge,
        [NotNull] string searchPattern = "*")
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentNullException(nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            return 0;
        }

        int deleted = 0;
        DateTime cutoff = DateTime.UtcNow - maxAge;

        EnumerationOptions opts = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true
        };

        try
        {
            foreach (string filePath in Directory.EnumerateFiles(directoryPath, searchPattern, opts))
            {
                try
                {
                    FileInfo fi = new(filePath);
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
    [return: NotNull]
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
                string test = Path.Join(path, "test_" + Guid.NewGuid().ToString("N") + ".tmp");
                using (File.Create(test)) { }
                File.Delete(test);
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
    /// <param name="path">
    /// The base path override to use for subsequent directory resolution.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void SetBasePathOverride(
        [MaybeNull] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        _basePathOverride = path;
    }

    /// <summary>
    /// Enumerates files from a directory with optional recursion.
    /// </summary>
    /// <param name="directory">
    /// The directory to enumerate files from.
    /// </param>
    /// <param name="searchPattern">
    /// The search pattern used to filter returned files.
    /// </param>
    /// <param name="recursive">
    /// <c>true</c> to include subdirectories; otherwise, <c>false</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    [return: NotNull]
    public static IEnumerable<string> EnumerateFiles(
        [MaybeNull] string directory,
        [NotNull] string searchPattern = "*",
        [NotNull] bool recursive = false)
    {
        return string.IsNullOrWhiteSpace(directory)
            ? throw new ArgumentNullException(nameof(directory))
            : EnumerateFilesCore();
        IEnumerable<string> EnumerateFilesCore()
        {
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            EnumerationOptions opts = new()
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true
            };

            foreach (string file in Directory.EnumerateFiles(directory, searchPattern, opts))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Computes the size of a directory in bytes.
    /// </summary>
    /// <param name="directoryPath">
    /// The directory whose total file size should be calculated.
    /// </param>
    /// <param name="includeSubdirectories">
    /// <c>true</c> to include files in subdirectories; otherwise, <c>false</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    [return: NotNull]
    public static long CalculateDirectorySize(
        [MaybeNull] string directoryPath,
        [NotNull] bool includeSubdirectories = true)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentNullException(nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            return 0;
        }

        long size = 0;

        try
        {
            EnumerationOptions opts = new()
            {
                RecurseSubdirectories = includeSubdirectories,
                IgnoreInaccessible = true
            };

            foreach (string file in Directory.EnumerateFiles(directoryPath, "*", opts))
            {
                try { size += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }

        return size;
    }

    /// <summary>
    /// Creates a date-based directory (yyyy-MM-dd) under the specified parent path.
    /// </summary>
    /// <param name="parentPath">
    /// The parent directory where the date-based directory should be created.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    [return: NotNull]
    public static string CreateDateDirectory(
        [MaybeNull] string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentNullException(nameof(parentPath));
        }

        string dir = Path.Join(parentPath, DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        ENSURE_DIRECTORY_EXISTS(dir);
        return dir;
    }

    /// <summary>
    /// Creates a hierarchical date-based directory (yyyy/MM/dd) under the specified parent path.
    /// </summary>
    /// <param name="parentPath">
    /// The parent directory where the hierarchical date-based directory should be created.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static string CreateHierarchicalDateDirectory(
        [MaybeNull] string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentNullException(nameof(parentPath));
        }

        DateTime now = DateTime.UtcNow;
        string year = Path.Join(parentPath, now.ToString("yyyy", CultureInfo.InvariantCulture));
        string month = Path.Join(year, now.ToString("MM", CultureInfo.InvariantCulture));
        string day = Path.Join(month, now.ToString("dd", CultureInfo.InvariantCulture));

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
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [return: NotNull]
    public static string EnsureShardedPath(
        [MaybeNull] string parentPath,
        [MaybeNull] string key,
        [NotNull] int depth = 2,
        [NotNull] int width = 2)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentNullException(nameof(parentPath));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (depth < 1 || width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
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
                string seg = (h & mask).ToString("X" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                p = Path.Join(p, seg);
                h >>= width * 4;
            }
            ENSURE_DIRECTORY_EXISTS(p);
            return Path.Join(p, key);
        }
    }
}
