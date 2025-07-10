namespace Nalix.Common.Environment;

/// <summary>
/// Class that defines default directories for the application with enhanced functionality
/// and flexibility for both development and production environments.
/// </summary>
public static partial class Directories
{
    #region Fields

    // Thread-safe directory creation lock
    private static readonly System.Threading.ReaderWriterLockSlim DirectoryLock =
        new(System.Threading.LockRecursionPolicy.SupportsRecursion);

    // Directory creation event (nullable to resolve CS8618)
    private static event System.Action<System.String> DirectoryCreated;

    // Flag to indicate if we're running in a container
    private static readonly System.Lazy<System.Boolean> IsContainerLazy = new(() =>
        System.IO.File.Exists("/.dockerenv") ||
        (System.IO.File.Exists("/proc/1/cgroup") &&
        System.IO.File.ReadAllText("/proc/1/cgroup").Contains("docker")));

    // For testing purposes, to override base path (nullable to resolve CS8618)
    private static string _basePathOverride;

    // Lazy-initialized paths
    private static readonly System.Lazy<System.String> BasePathLazy = new(() =>
    {
        if (_basePathOverride != null)
            return _basePathOverride;

        // Handle Docker and Kubernetes environments by using /app as base path
        if (IsContainerLazy.Value && System.IO.Directory.Exists("/assets"))
            return "/assets";

        // Standard to the application's base directory
        return System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory
                             .TrimEnd(System.IO.Path.DirectorySeparatorChar), "assets");
    });

    private static readonly System.Lazy<System.String> DataPathLazy = new(() =>
    {
        string path;

        // In container environments, prefer using a mounted volume if available
        if (IsContainerLazy.Value &&
            System.IO.Directory.Exists("/data"))
        {
            path = "/data";
        }
        else
        {
            path = System.IO.Path.Combine(BasePathLazy.Value, "data");
        }

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> LogsPathLazy = new(() =>
    {
        string path;

        // In container environments, prefer using a mounted volume if available
        if (IsContainerLazy.Value &&
            System.IO.Directory.Exists("/logs"))
        {
            path = "/logs";
        }
        else
        {
            path = System.IO.Path.Combine(DataPathLazy.Value, "logs");
        }

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> TempPathLazy = new(() =>
    {
        string path;

        // In container environments, prefer using a mounted volume if available
        if (IsContainerLazy.Value &&
            System.IO.Directory.Exists("/tmp"))
        {
            path = "/tmp";
        }
        else
        {
            path = System.IO.Path.Combine(DataPathLazy.Value, "tmp");
        }

        EnsureDirectoryExists(path);

        // Automatically clean up old files in the temp directory
        CleanupDirectory(path, System.TimeSpan.FromDays(7));

        return path;
    });

    private static readonly System.Lazy<System.String> ConfigPathLazy = new(() =>
    {
        string path;

        // In container environments, prefer using a mounted volume if available
        if (IsContainerLazy.Value &&
            System.IO.Directory.Exists("/config"))
        {
            path = "/config";
        }
        else
        {
            path = System.IO.Path.Combine(DataPathLazy.Value, "config");
        }

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> StoragePathLazy = new(() =>
    {
        string path;

        // In container environments, prefer using a mounted volume if available
        if (IsContainerLazy.Value &&
            System.IO.Directory.Exists("/storage"))
        {
            path = "/storage";
        }
        else
        {
            path = System.IO.Path.Combine(DataPathLazy.Value, "storage");
        }

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> DatabasePathLazy = new(() =>
    {
        string path;

        // In container environments, prefer using a mounted volume if available
        if (IsContainerLazy.Value &&
            System.IO.Directory.Exists("/db"))
        {
            path = "/db";
        }
        else
        {
            path = System.IO.Path.Combine(DataPathLazy.Value, "db");
        }

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> CachesPathLazy = new(() =>
    {
        string path = System.IO.Path.Combine(DataPathLazy.Value, "caches");
        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> UploadsPathLazy = new(() =>
    {
        string path = System.IO.Path.Combine(DataPathLazy.Value, "uploads");
        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> BackupsPathLazy = new(() =>
    {
        string path = System.IO.Path.Combine(DataPathLazy.Value, "backups");
        EnsureDirectoryExists(path);
        return path;
    });

    #endregion Fields

    #region Properties

    /// <summary>
    /// The base directory of the application.
    /// </summary>
    public static string BasePath => BasePathLazy.Value;

    /// <summary>
    /// Directory for storing log files.
    /// </summary>
    public static string LogsPath => LogsPathLazy.Value;

    /// <summary>
    /// Directory for storing application data files.
    /// </summary>
    public static string DataPath => DataPathLazy.Value;

    /// <summary>
    /// Directory for storing system configuration files.
    /// </summary>
    public static string ConfigPath => ConfigPathLazy.Value;

    /// <summary>
    /// Directory for storing temporary files.
    /// These files may be automatically cleaned up periodically.
    /// </summary>
    public static string TempPath => TempPathLazy.Value;

    /// <summary>
    /// Directory for storing persistent data.
    /// </summary>
    public static string StoragePath => StoragePathLazy.Value;

    /// <summary>
    /// Directory for storing database files.
    /// </summary>
    public static string DatabasePath => DatabasePathLazy.Value;

    /// <summary>
    /// Directory for storing cache files.
    /// </summary>
    public static string CachesPath => CachesPathLazy.Value;

    /// <summary>
    /// Directory for storing uploaded files.
    /// </summary>
    public static string UploadsPath => UploadsPathLazy.Value;

    /// <summary>
    /// Directory for storing backup files.
    /// </summary>
    public static string BackupsPath => BackupsPathLazy.Value;

    /// <summary>
    /// Indicates if the application is running in a container environment.
    /// </summary>
    public static bool IsContainer => IsContainerLazy.Value;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Static constructor to initialize the default directories.
    /// Ensures that all necessary directories are created.
    /// </summary>
    static Directories()
    {
        // Access all properties to ensure directories are created
        _ = LogsPath;
        _ = DataPath;
        _ = ConfigPath;
        _ = TempPath;
        _ = StoragePath;
        _ = DatabasePath;
        _ = CachesPath;
        _ = UploadsPath;
        _ = BackupsPath;
    }

    #endregion Constructors
}