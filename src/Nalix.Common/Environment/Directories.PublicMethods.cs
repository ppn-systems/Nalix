namespace Nalix.Common.Environment;

public static partial class Directories
{
    #region Public Methods

    /// <summary>
    /// Register a handler for directory creation events.
    /// </summary>
    /// <param name="handler">The handler to call when a directory is created.</param>
    public static void RegisterDirectoryCreationHandler(System.Action<System.String> handler)
        => DirectoryCreated += handler;

    /// <summary>
    /// Unregister a handler for directory creation events.
    /// </summary>
    /// <param name="handler">The handler to remove.</param>
    public static void UnregisterDirectoryCreationHandler(System.Action<System.String> handler)
        => DirectoryCreated -= handler;

    /// <summary>
    /// Creates a subdirectory within the specified parent directory if it doesn't exist.
    /// </summary>
    /// <param name="parentPath">The parent directory path.</param>
    /// <param name="directoryName">The name of the subdirectory to create.</param>
    /// <returns>The full path to the created directory.</returns>
    public static System.String CreateSubdirectory(System.String parentPath, System.String directoryName)
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        if (System.String.IsNullOrWhiteSpace(directoryName))
        {
            throw new System.ArgumentNullException(nameof(directoryName));
        }

        System.String fullPath = System.IO.Path.Combine(parentPath, directoryName);
        EnsureDirectoryExists(fullPath);

        return fullPath;
    }

    /// <summary>
    /// Creates a timestamped subdirectory within the specified parent directory.
    /// </summary>
    /// <param name="parentPath">The parent directory path.</param>
    /// <param name="prefix">An optional prefix for the directory name.</param>
    /// <returns>The full path to the created directory.</returns>
    public static System.String CreateTimestampedDirectory(System.String parentPath, System.String prefix = "")
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        // Create a directory name with the current timestamp
        System.String timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        System.String directoryName = System.String.IsNullOrEmpty(prefix) ? timestamp : $"{prefix}_{timestamp}";

        return CreateSubdirectory(parentPath, directoryName);
    }

    /// <summary>
    /// Gets a path to a file within one of the application's directories.
    /// </summary>
    /// <param name="directoryPath">The base directory path.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the file.</returns>
    public static System.String GetFilePath(System.String directoryPath, System.String fileName)
    {
        if (System.String.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (System.String.IsNullOrWhiteSpace(fileName))
        {
            throw new System.ArgumentNullException(nameof(fileName));
        }

        EnsureDirectoryExists(directoryPath);
        return System.IO.Path.Combine(directoryPath, fileName);
    }

    /// <summary>
    /// Gets a path to a temporary file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the temporary file.</returns>
    public static System.String GetTempFilePath(System.String fileName) => GetFilePath(TempPath, fileName);

    /// <summary>
    /// Gets a path to a timestamped file in the specified directory.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="fileNameBase">The base file name (without extension).</param>
    /// <param name="extension">The file extension (without the dot).</param>
    /// <returns>The full path to the timestamped file.</returns>
    public static System.String GetTimestampedFilePath(System.String directoryPath, System.String fileNameBase, System.String extension)
    {
        if (System.String.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (System.String.IsNullOrWhiteSpace(fileNameBase))
        {
            throw new System.ArgumentNullException(nameof(fileNameBase));
        }

        System.String timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        System.String fileName = $"{fileNameBase}_{timestamp}.{extension.TrimStart('.')}";

        return GetFilePath(directoryPath, fileName);
    }

    /// <summary>
    /// Gets a path to a log file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the log file.</returns>
    public static System.String GetLogFilePath(System.String fileName) => GetFilePath(LogsPath, fileName);

    /// <summary>
    /// Gets a path to a config file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the config file.</returns>
    public static System.String GetConfigFilePath(System.String fileName) => GetFilePath(ConfigPath, fileName);

    /// <summary>
    /// Gets a path to a storage file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the storage file.</returns>
    public static System.String GetStorageFilePath(System.String fileName) => GetFilePath(StoragePath, fileName);

    /// <summary>
    /// Gets a path to a database file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the database file.</returns>
    public static System.String GetDatabaseFilePath(System.String fileName) => GetFilePath(DatabasePath, fileName);

    /// <summary>
    /// Cleans up files in the specified directory that are older than the given timespan.
    /// </summary>
    /// <param name="directoryPath">The directory to clean.</param>
    /// <param name="maxAge">The maximum age of files to keep.</param>
    /// <param name="searchPattern">The search pattern for files to consider.</param>
    /// <returns>The TransportProtocol of files deleted.</returns>
    public static System.Int32 CleanupDirectory(System.String directoryPath, System.TimeSpan maxAge, System.String searchPattern = "*")
    {
        if (System.String.IsNullOrWhiteSpace(directoryPath))
        {
            throw new System.ArgumentNullException(nameof(directoryPath));
        }

        if (!System.IO.Directory.Exists(directoryPath))
        {
            return 0;
        }

        System.Int32 deletedCount = 0;
        System.DateTime cutoff = System.DateTime.UtcNow - maxAge;

        try
        {
            System.IO.DirectoryInfo di = new(directoryPath);

            // Process files
            foreach (System.IO.FileInfo file in di.GetFiles(searchPattern))
            {
                if (file.LastWriteTimeUtc < cutoff)
                {
                    try
                    {
                        file.Delete();
                        deletedCount++;
                    }
                    catch
                    {
                        // Ignore errors when deleting files
                    }
                }
            }
        }
        catch
        {
            // Ignore any errors during cleanup
        }

        return deletedCount;
    }

    /// <summary>
    /// Ensures all application directories exist and have proper permissions.
    /// </summary>
    /// <returns>True if all directories are accessible, false otherwise.</returns>
    public static System.Boolean ValidateDirectories()
    {
        try
        {
            // Test write access to all key directories
            System.String[] testPaths =
            [
                LogsPath,
                DataPath,
                ConfigPath,
                TempPath,
                StoragePath,
                DatabasePath,
                CachesPath,
                UploadsPath,
                BackupsPath
            ];

            foreach (System.String path in testPaths)
            {
                // Test write access by creating and immediately deleting a temporary file
                System.String testFile = System.IO.Path.Combine(path, $"test_{System.Guid.NewGuid():N}.tmp");
                using (System.IO.File.Create(testFile)) { }
                System.IO.File.Delete(testFile);
            }

            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Temporarily overrides the base path for testing purposes.
    /// Will be reset after application restart.
    /// </summary>
    /// <param name="path">The path to use as the base path.</param>
    public static void OverrideBasePathForTesting(System.String path)
    {
        if (System.String.IsNullOrWhiteSpace(path))
        {
            throw new System.ArgumentNullException(nameof(path));
        }

        _basePathOverride = path;
    }

    /// <summary>
    /// Gets all files in a directory matching a pattern, with optional recursive search.
    /// </summary>
    /// <param name="directory">The directory to search.</param>
    /// <param name="searchPattern">The search pattern to use.</param>
    /// <param name="recursive">Whether to search subdirectories recursively.</param>
    /// <returns>A collection of file paths.</returns>
    public static System.Collections.Generic.IEnumerable<System.String> GetFiles(
        System.String directory, System.String searchPattern = "*", System.Boolean recursive = false)
    {
        if (System.String.IsNullOrWhiteSpace(directory))
        {
            throw new System.ArgumentNullException(nameof(directory));
        }

        return !System.IO.Directory.Exists(directory)
            ? []
            : (System.Collections.Generic.IEnumerable<System.String>)System.IO.Directory.GetFiles(directory, searchPattern,
            recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
    }

    /// <summary>
    /// Gets the size of a directory in bytes, optionally including subdirectories.
    /// </summary>
    /// <param name="directoryPath">The directory to calculate the size for.</param>
    /// <param name="includeSubdirectories">Whether to include subdirectories in the calculation.</param>
    /// <returns>The total size in bytes.</returns>
    public static System.Int64 GetDirectorySize(System.String directoryPath, System.Boolean includeSubdirectories = true)
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

        // Add size of files in the current directory
        foreach (System.String file in System.IO.Directory.GetFiles(directoryPath))
        {
            try
            {
                size += new System.IO.FileInfo(file).Length;
            }
            catch
            {
                // Ignore files that can't be accessed
            }
        }

        // Add size of subdirectories if requested
        if (includeSubdirectories)
        {
            foreach (System.String subdirectory in System.IO.Directory.GetDirectories(directoryPath))
            {
                try
                {
                    size += GetDirectorySize(subdirectory, true);
                }
                catch
                {
                    // Ignore directories that can't be accessed
                }
            }
        }

        return size;
    }

    /// <summary>
    /// Creates a subdirectory for today's date in the specified parent directory.
    /// Format: YYYY-MM-DD
    /// </summary>
    /// <param name="parentPath">The parent directory path.</param>
    /// <returns>The path to the date-based subdirectory.</returns>
    public static System.String CreateDateDirectory(System.String parentPath)
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        System.String datePath = System.IO.Path.Combine(parentPath, System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
        EnsureDirectoryExists(datePath);
        return datePath;
    }

    /// <summary>
    /// Creates a hierarchical directory structure based on the current date.
    /// Format: YYYY/MM/DD
    /// </summary>
    /// <param name="parentPath">The parent directory path.</param>
    /// <returns>The path to the hierarchical date-based directory.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String CreateHierarchicalDateDirectory(System.String parentPath)
    {
        if (System.String.IsNullOrWhiteSpace(parentPath))
        {
            throw new System.ArgumentNullException(nameof(parentPath));
        }

        System.DateTime now = System.DateTime.UtcNow;
        System.String yearPath = System.IO.Path.Combine(parentPath, now.ToString("yyyy"));
        System.String monthPath = System.IO.Path.Combine(yearPath, now.ToString("MM"));
        System.String dayPath = System.IO.Path.Combine(monthPath, now.ToString("dd"));

        EnsureDirectoryExists(dayPath);
        return dayPath;
    }

    #endregion Public Methods
}