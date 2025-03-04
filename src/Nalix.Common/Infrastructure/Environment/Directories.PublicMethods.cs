// Copyright (c) 2025 PPN Corporation. All rights reserved.

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
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Action<System.String> handler)
    {
        System.ArgumentNullException.ThrowIfNull(handler);
        Directories.DirectoryCreated += handler;
    }

    /// <summary>
    /// Unregisters a directory creation event handler.
    /// </summary>
    /// <param name="handler">
    /// The handler to remove.
    /// </param>
    public static void UnregisterDirectoryCreationHandler(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Action<System.String> handler)
    {
        System.ArgumentNullException.ThrowIfNull(handler);
        Directories.DirectoryCreated -= handler;
    }

    // -------- Helpers to build paths --------

    /// <summary>
    /// Creates a subdirectory under the specified parent directory, if it does not exist.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String CreateSubdirectory(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String parentPath,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String directoryName)
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        if (System.String.IsNullOrWhiteSpace(directoryName))
        {
            throw new System.ArgumentNullException(nameof(directoryName));
        }

        System.String fullPath = COMBINE_SAFE(parentPath, directoryName);
        ENSURE_DIRECTORY_EXISTS(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates a subdirectory with a UTC timestamp-based name (yyyyMMddTHHmmssZ).
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String CreateTimestampedDirectory(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String parentPath,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String prefix = "")
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        System.String timestamp = System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        System.String directoryName = System.String.IsNullOrEmpty(prefix) ? timestamp : (prefix + "_" + timestamp);

        return CreateSubdirectory(parentPath, directoryName);
    }

    /// <summary>
    /// Returns a full file path under a given directory, ensuring the directory exists.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String GetFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String directoryPath,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String fileName)
    {
        if (System.String.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (System.String.IsNullOrWhiteSpace(fileName))
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
    public static System.String GetTempFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String fileName) => GetFilePath(TemporaryDirectory, fileName);

    /// <summary>
    /// Returns a timestamped file path (yyyyMMddTHHmmssZ) under a directory.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String GetTimestampedFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String directoryPath,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String fileNameBase,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String extension)
    {
        if (System.String.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (System.String.IsNullOrWhiteSpace(fileNameBase))
        {
            throw new System.ArgumentNullException(nameof(fileNameBase));
        }

        System.String timestamp = System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        System.String fileName = fileNameBase + "_" + timestamp + "." + extension.TrimStart('.');

        return GetFilePath(directoryPath, fileName);
    }

    /// <summary>
    /// Returns a log file path under <see cref="LogsDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String GetLogFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String fileName) => GetFilePath(LogsDirectory, fileName);

    /// <summary>
    /// Returns a config file path under <see cref="ConfigurationDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String GetConfigFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String fileName) => GetFilePath(ConfigurationDirectory, fileName);

    /// <summary>
    /// Returns a storage file path under <see cref="StorageDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String GetStorageFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String fileName) => GetFilePath(StorageDirectory, fileName);

    /// <summary>
    /// Returns a database file path under <see cref="DatabaseDirectory"/>.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String GetDatabaseFilePath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String fileName) => GetFilePath(DatabaseDirectory, fileName);

    // -------- Maintenance --------

    /// <summary>Deletes files older than the specified age in a directory.</summary>
    /// <param name="directoryPath">The directory to clean.</param>
    /// <param name="maxAge">The maximum age to keep.</param>
    /// <param name="searchPattern">Glob pattern to select files.</param>
    /// <returns>Number of files deleted.</returns>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int32 DeleteOldFiles(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String directoryPath,
        [System.Diagnostics.CodeAnalysis.NotNull] System.TimeSpan maxAge,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String searchPattern = "*")
    {
        if (System.String.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (!System.IO.Directory.Exists(directoryPath))
        {
            return 0;
        }

        System.Int32 deleted = 0;
        System.DateTime cutoff = System.DateTime.UtcNow - maxAge;

        System.IO.EnumerationOptions opts = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true
        };

        try
        {
            foreach (System.String filePath in System.IO.Directory.EnumerateFiles(directoryPath, searchPattern, opts))
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
    public static System.Boolean CanAccessAllDirectories()
    {
        try
        {
            System.String[] testPaths =
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

            for (System.Int32 i = 0; i < testPaths.Length; i++)
            {
                System.String path = testPaths[i];
                System.String test = System.IO.Path.Join(path, "test_" + System.Guid.NewGuid().ToString("N") + ".tmp");
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
    public static void SetBasePathOverride(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String path)
    {
        if (System.String.IsNullOrWhiteSpace(path))
        {
            throw new System.ArgumentNullException(nameof(path));
        }

        _basePathOverride = path;
    }

    /// <summary>
    /// Enumerates files from a directory with optional recursion.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Collections.Generic.IEnumerable<System.String> EnumerateFiles(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String directory,
        [System.Diagnostics.CodeAnalysis.NotNull] System.String searchPattern = "*",
        [System.Diagnostics.CodeAnalysis.NotNull] System.Boolean recursive = false)
    {
        if (System.String.IsNullOrWhiteSpace(directory))
        {
            throw new System.ArgumentNullException(nameof(directory));
        }

        return EnumerateFilesCore();

        System.Collections.Generic.IEnumerable<System.String> EnumerateFilesCore()
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

            foreach (System.String file in System.IO.Directory.EnumerateFiles(directory, searchPattern, opts))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Computes the size of a directory in bytes.
    /// </summary>
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Int64 CalculateDirectorySize(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String directoryPath,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Boolean includeSubdirectories = true)
    {
        if (System.String.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (!System.IO.Directory.Exists(directoryPath))
        {
            return 0;
        }

        System.Int64 size = 0;

        try
        {
            System.IO.EnumerationOptions opts = new()
            {
                RecurseSubdirectories = includeSubdirectories,
                IgnoreInaccessible = true
            };

            foreach (System.String file in System.IO.Directory.EnumerateFiles(directoryPath, "*", opts))
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
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String CreateDateDirectory(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String parentPath)
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        System.String dir = System.IO.Path.Join(parentPath, System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
        ENSURE_DIRECTORY_EXISTS(dir);
        return dir;
    }

    /// <summary>
    /// Creates a hierarchical date-based directory (yyyy/MM/dd) under the specified parent path.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String CreateHierarchicalDateDirectory(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String parentPath)
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        System.DateTime now = System.DateTime.UtcNow;
        System.String year = System.IO.Path.Join(parentPath, now.ToString("yyyy"));
        System.String month = System.IO.Path.Join(year, now.ToString("MM"));
        System.String day = System.IO.Path.Join(month, now.ToString("dd"));

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
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.String EnsureShardedPath(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String parentPath,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.String key,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 depth = 2,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 width = 2)
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        if (System.String.IsNullOrWhiteSpace(key))
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
            System.UInt32 h = 2166136261;
            for (System.Int32 i = 0; i < key.Length; i++)
            {
                h ^= key[i];
                h *= 16777619;
            }

            System.String p = parentPath;
            for (System.Int32 i = 0; i < depth; i++)
            {
                System.UInt32 mask = (System.UInt32)((1 << (width * 4)) - 1);
                System.String seg = (h & mask).ToString("X" + width.ToString());
                p = System.IO.Path.Join(p, seg);
                h >>= width * 4;
            }
            ENSURE_DIRECTORY_EXISTS(p);
            return System.IO.Path.Join(p, key);
        }
    }
}
