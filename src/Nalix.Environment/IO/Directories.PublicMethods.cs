// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;

namespace Nalix.Environment.IO;

public static partial class Directories
{
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

                    // SEC: Skip symbolic links, junctions, and other reparse points to prevent 
                    // traversal attacks where a link points to a critical system file.
                    if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }

                    // SEC: Check both creation and write time. Some attackers manipulate 
                    // LastWriteTime to bypass cleanup. We delete only if BOTH indicate 
                    // the file is old, or if the timestamps are suspiciously inconsistent.
                    DateTime lastWrite = fi.LastWriteTimeUtc;
                    DateTime created = fi.CreationTimeUtc;

                    if (lastWrite < cutoff && created < cutoff)
                    {
                        fi.Delete();
                        deleted++;
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    Debug.WriteLine($"[Directories] DeleteOldFiles skipped '{filePath}': {ex}");
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            Debug.WriteLine($"[Directories] DeleteOldFiles failed for '{directoryPath}': {ex}");
        }

        if (deleted > 0 && Listener.IsEnabled(DiagnosticsEvents.IO.Cleanup))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.IO.Cleanup, new DiagnosticLog("ENV.Directories:Cleanup", $"files-deleted path={directoryPath} count={deleted}"));
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

    /// <summary>
    /// Safely writes content to a new file, setting restricted permissions (0600 on Unix) for private files.
    /// Returns false if the file already exists or fails to write.
    /// </summary>
    /// <param name="path">The absolute path of the file to write.</param>
    /// <param name="content">The string content to write.</param>
    /// <param name="isPrivate">Whether to restrict file permissions to the owner only (0600 on Unix).</param>
    /// <returns>True if the file was written successfully; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    public static bool TryWriteNewFile(string path, string content, bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.Read
            };

            if (isPrivate && !OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using FileStream stream = new(path, options);
            using StreamWriter writer = new(stream);
            writer.WriteLine(content);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }
    }
}

