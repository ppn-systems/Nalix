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
    private static System.String _basePathOverride;

    // Lazy-initialized paths
    private static readonly System.Lazy<System.String> BasePathLazy = new(() =>
    {
        if (_basePathOverride != null)
        {
            return _basePathOverride;
        }

        // Handle Docker and Kubernetes environments by using /app as base path
        if (IsContainerLazy.Value && System.IO.Directory.Exists("/assets"))
        {
            return "/assets";
        }

        // Standard to the application's base directory
        return System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory
                             .TrimEnd(System.IO.Path.DirectorySeparatorChar), "assets");
    });

    private static readonly System.Lazy<System.String> DataPathLazy = new(() =>
    {
        System.String path = IsContainerLazy.Value &&
            System.IO.Directory.Exists("/data")
            ? "/data"
            : System.IO.Path.Combine(BasePathLazy.Value, "data");

        // In container environments, prefer using a mounted volume if available

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> LogsPathLazy = new(() =>
    {
        System.String path = IsContainerLazy.Value &&
            System.IO.Directory.Exists("/logs")
            ? "/logs"
            : System.IO.Path.Combine(DataPathLazy.Value, "logs");

        // In container environments, prefer using a mounted volume if available

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> TempPathLazy = new(() =>
    {
        System.String path = IsContainerLazy.Value &&
            System.IO.Directory.Exists("/tmp")
            ? "/tmp"
            : System.IO.Path.Combine(DataPathLazy.Value, "tmp");

        // In container environments, prefer using a mounted volume if available

        EnsureDirectoryExists(path);

        // Automatically clean up old files in the temp directory
        _ = CleanupDirectory(path, System.TimeSpan.FromDays(7));

        return path;
    });

    private static readonly System.Lazy<System.String> ConfigPathLazy = new(() =>
    {
        System.String path = IsContainerLazy.Value &&
            System.IO.Directory.Exists("/config")
            ? "/config"
            : System.IO.Path.Combine(DataPathLazy.Value, "config");

        // In container environments, prefer using a mounted volume if available

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> StoragePathLazy = new(() =>
    {
        System.String path = IsContainerLazy.Value &&
            System.IO.Directory.Exists("/storage")
            ? "/storage"
            : System.IO.Path.Combine(DataPathLazy.Value, "storage");

        // In container environments, prefer using a mounted volume if available

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> DatabasePathLazy = new(() =>
    {
        System.String path = IsContainerLazy.Value &&
            System.IO.Directory.Exists("/db")
            ? "/db"
            : System.IO.Path.Combine(DataPathLazy.Value, "db");

        // In container environments, prefer using a mounted volume if available

        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> CachesPathLazy = new(() =>
    {
        System.String path = System.IO.Path.Combine(DataPathLazy.Value, "caches");
        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> UploadsPathLazy = new(() =>
    {
        System.String path = System.IO.Path.Combine(DataPathLazy.Value, "uploads");
        EnsureDirectoryExists(path);
        return path;
    });

    private static readonly System.Lazy<System.String> BackupsPathLazy = new(() =>
    {
        System.String path = System.IO.Path.Combine(DataPathLazy.Value, "backups");
        EnsureDirectoryExists(path);
        return path;
    });

    #endregion Fields

    #region Properties

    /// <summary>
    /// The base directory of the application.
    /// </summary>
    public static System.String BasePath => BasePathLazy.Value;

    /// <summary>
    /// Directory for storing log files.
    /// </summary>
    public static System.String LogsPath => LogsPathLazy.Value;

    /// <summary>
    /// Directory for storing application data files.
    /// </summary>
    public static System.String DataPath => DataPathLazy.Value;

    /// <summary>
    /// Directory for storing system configuration files.
    /// </summary>
    public static System.String ConfigPath => ConfigPathLazy.Value;

    /// <summary>
    /// Directory for storing temporary files.
    /// These files may be automatically cleaned up periodically.
    /// </summary>
    public static System.String TempPath => TempPathLazy.Value;

    /// <summary>
    /// Directory for storing persistent data.
    /// </summary>
    public static System.String StoragePath => StoragePathLazy.Value;

    /// <summary>
    /// Directory for storing database files.
    /// </summary>
    public static System.String DatabasePath => DatabasePathLazy.Value;

    /// <summary>
    /// Directory for storing cache files.
    /// </summary>
    public static System.String CachesPath => CachesPathLazy.Value;

    /// <summary>
    /// Directory for storing uploaded files.
    /// </summary>
    public static System.String UploadsPath => UploadsPathLazy.Value;

    /// <summary>
    /// Directory for storing backup files.
    /// </summary>
    public static System.String BackupsPath => BackupsPathLazy.Value;

    /// <summary>
    /// Indicates if the application is running in a container environment.
    /// </summary>
    public static System.Boolean IsContainer => IsContainerLazy.Value;

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