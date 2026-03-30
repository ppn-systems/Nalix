// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;

namespace Nalix.Environment.IO;

public static partial class Directories
{
    #region Events

    /// <summary>
    /// Registers a directory creation event handler.
    /// </summary>
    /// <param name="handler">
    /// The handler to invoke when a directory is created.
    /// </param>
    public static void RegisterDirectoryCreationHandler(Action<string> handler)
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
    public static void UnregisterDirectoryCreationHandler(Action<string> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        DirectoryCreated -= handler;
    }

    #endregion Events

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
    public static string GetFilePath(string directoryPath, string fileName)
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

    /// <summary>Deletes files older than the specified age in a directory.</summary>
    /// <param name="directoryPath">The directory to clean.</param>
    /// <param name="maxAge">The maximum age to keep.</param>
    /// <param name="searchPattern">Glob pattern to select files.</param>
    /// <returns>Number of files deleted.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static int DeleteOldFiles(string directoryPath, TimeSpan maxAge, string searchPattern = "*")
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
                catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                {
                    Debug.WriteLine($"[Directories] DeleteOldFiles skipped '{filePath}': {ex}");
                }
            }
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            Debug.WriteLine($"[Directories] DeleteOldFiles failed for '{directoryPath}': {ex}");
        }

        return deleted;
    }

    /// <summary>
    /// Validates that all known directories are accessible by writing a temporary file.
    /// </summary>
    /// <returns>
    /// <c>true</c> if all directories accept writes; otherwise <c>false</c>.
    /// </returns>
    public static bool CanAccessAllDirectories()
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
            if (!HAS_WRITE_ACCESS(testPaths[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Overrides the base path for testing. The override is not persisted across process restarts.
    /// </summary>
    /// <param name="path">
    /// The base path override to use for subsequent directory resolution.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void SetBasePathOverride(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        s_basePathOverride = path;
        RESET_LAZIES();
    }
}
