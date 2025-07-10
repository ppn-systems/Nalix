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
    public static string CreateSubdirectory(string parentPath, string directoryName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
            throw new System.ArgumentNullException(nameof(parentPath));

        if (string.IsNullOrWhiteSpace(directoryName))
            throw new System.ArgumentNullException(nameof(directoryName));

        string fullPath = System.IO.Path.Combine(parentPath, directoryName);
        EnsureDirectoryExists(fullPath);

        return fullPath;
    }

    /// <summary>
    /// Creates a timestamped subdirectory within the specified parent directory.
    /// </summary>
    /// <param name="parentPath">The parent directory path.</param>
    /// <param name="prefix">An optional prefix for the directory name.</param>
    /// <returns>The full path to the created directory.</returns>
    public static string CreateTimestampedDirectory(string parentPath, string prefix = "")
    {
        if (string.IsNullOrWhiteSpace(parentPath))
            throw new System.ArgumentNullException(nameof(parentPath));

        // Create a directory name with the current timestamp
        string timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        string directoryName = string.IsNullOrEmpty(prefix) ? timestamp : $"{prefix}_{timestamp}";

        return CreateSubdirectory(parentPath, directoryName);
    }

    /// <summary>
    /// Gets a path to a file within one of the application's directories.
    /// </summary>
    /// <param name="directoryPath">The base directory path.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the file.</returns>
    public static string GetFilePath(string directoryPath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new System.ArgumentNullException(nameof(directoryPath));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new System.ArgumentNullException(nameof(fileName));

        EnsureDirectoryExists(directoryPath);
        return System.IO.Path.Combine(directoryPath, fileName);
    }

    /// <summary>
    /// Gets a path to a temporary file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the temporary file.</returns>
    public static string GetTempFilePath(string fileName) => GetFilePath(TempPath, fileName);

    /// <summary>
    /// Gets a path to a timestamped file in the specified directory.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="fileNameBase">The base file name (without extension).</param>
    /// <param name="extension">The file extension (without the dot).</param>
    /// <returns>The full path to the timestamped file.</returns>
    public static string GetTimestampedFilePath(string directoryPath, string fileNameBase, string extension)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new System.ArgumentNullException(nameof(directoryPath));

        if (string.IsNullOrWhiteSpace(fileNameBase))
            throw new System.ArgumentNullException(nameof(fileNameBase));

        string timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"{fileNameBase}_{timestamp}.{extension.TrimStart('.')}";

        return GetFilePath(directoryPath, fileName);
    }

    /// <summary>
    /// Gets a path to a log file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the log file.</returns>
    public static string GetLogFilePath(string fileName) => GetFilePath(LogsPath, fileName);

    /// <summary>
    /// Gets a path to a config file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the config file.</returns>
    public static string GetConfigFilePath(string fileName) => GetFilePath(ConfigPath, fileName);

    /// <summary>
    /// Gets a path to a storage file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the storage file.</returns>
    public static string GetStorageFilePath(string fileName) => GetFilePath(StoragePath, fileName);

    /// <summary>
    /// Gets a path to a database file with the given name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path to the database file.</returns>
    public static string GetDatabaseFilePath(string fileName) => GetFilePath(DatabasePath, fileName);

    /// <summary>
    /// Cleans up files in the specified directory that are older than the given timespan.
    /// </summary>
    /// <param name="directoryPath">The directory to clean.</param>
    /// <param name="maxAge">The maximum age of files to keep.</param>
    /// <param name="searchPattern">The search pattern for files to consider.</param>
    /// <returns>The Number of files deleted.</returns>
    public static int CleanupDirectory(string directoryPath, System.TimeSpan maxAge, string searchPattern = "*")
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new System.ArgumentNullException(nameof(directoryPath));

        if (!System.IO.Directory.Exists(directoryPath))
            return 0;

        int deletedCount = 0;
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
    public static bool ValidateDirectories()
    {
        try
        {
            // Test write access to all key directories
            string[] testPaths =
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

            foreach (string path in testPaths)
            {
                // Test write access by creating and immediately deleting a temporary file
                string testFile = System.IO.Path.Combine(path, $"test_{System.Guid.NewGuid():N}.tmp");
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
    public static void OverrideBasePathForTesting(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new System.ArgumentNullException(nameof(path));

        _basePathOverride = path;
    }

    /// <summary>
    /// Gets all files in a directory matching a pattern, with optional recursive search.
    /// </summary>
    /// <param name="directory">The directory to search.</param>
    /// <param name="searchPattern">The search pattern to use.</param>
    /// <param name="recursive">Whether to search subdirectories recursively.</param>
    /// <returns>A collection of file paths.</returns>
    public static System.Collections.Generic.IEnumerable<string> GetFiles(
        string directory, string searchPattern = "*", bool recursive = false)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new System.ArgumentNullException(nameof(directory));

        if (!System.IO.Directory.Exists(directory))
            return [];

        return System.IO.Directory.GetFiles(directory, searchPattern,
            recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
    }

    /// <summary>
    /// Gets the size of a directory in bytes, optionally including subdirectories.
    /// </summary>
    /// <param name="directoryPath">The directory to calculate the size for.</param>
    /// <param name="includeSubdirectories">Whether to include subdirectories in the calculation.</param>
    /// <returns>The total size in bytes.</returns>
    public static long GetDirectorySize(string directoryPath, bool includeSubdirectories = true)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new System.ArgumentNullException(nameof(directoryPath));

        if (!System.IO.Directory.Exists(directoryPath))
            return 0;

        long size = 0;

        // Add size of files in the current directory
        foreach (string file in System.IO.Directory.GetFiles(directoryPath))
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
            foreach (string subdirectory in System.IO.Directory.GetDirectories(directoryPath))
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
    public static string CreateDateDirectory(string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
            throw new System.ArgumentNullException(nameof(parentPath));

        string datePath = System.IO.Path.Combine(parentPath, System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
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
    public static string CreateHierarchicalDateDirectory(string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
            throw new System.ArgumentNullException(nameof(parentPath));

        System.DateTime now = System.DateTime.UtcNow;
        string yearPath = System.IO.Path.Combine(parentPath, now.ToString("yyyy"));
        string monthPath = System.IO.Path.Combine(yearPath, now.ToString("MM"));
        string dayPath = System.IO.Path.Combine(monthPath, now.ToString("dd"));

        EnsureDirectoryExists(dayPath);
        return dayPath;
    }

    #endregion Public Methods
}