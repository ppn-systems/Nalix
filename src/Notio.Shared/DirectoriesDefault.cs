using System;
using System.IO;

namespace Notio.Shared;

/// <summary>
/// Class that defines default directories for the application.
/// </summary>
public static class DirectoriesDefault
{
    private static readonly Lazy<string> BasePathLazy = new(() =>
        AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));

    private static readonly Lazy<string> DataPathLazy = new(() =>
    {
        string path = Path.Combine(BasePathLazy.Value, "Data");
        Directory.CreateDirectory(path);
        return path;
    });

    private static readonly Lazy<string> LogsPathLazy = new(() =>
    {
        string path = Path.Combine(DataPathLazy.Value, "Logs");
        Directory.CreateDirectory(path);
        return path;
    });

    private static readonly Lazy<string> TempPathLazy = new(() =>
    {
        string path = Path.Combine(DataPathLazy.Value, "Temp");
        Directory.CreateDirectory(path);
        return path;
    });

    private static readonly Lazy<string> ConfigPathLazy = new(() =>
    {
        string path = Path.Combine(DataPathLazy.Value, "Config");
        Directory.CreateDirectory(path);
        return path;
    });

    private static readonly Lazy<string> StoragePathLazy = new(() =>
    {
        string path = Path.Combine(DataPathLazy.Value, "Storage");
        Directory.CreateDirectory(path);
        return path;
    });


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
    /// </summary>
    public static string TempPath => TempPathLazy.Value;

    /// <summary>
    /// Directory for storing metric-related files.
    /// </summary>
    public static string StoragePath => StoragePathLazy.Value;

    /// <summary>
    /// Static constructor to initialize the default directories.
    /// Ensures that all necessary directories are created.
    /// </summary>
    static DirectoriesDefault()
        => EnsureDirectoriesExist(LogsPath, DataPath, ConfigPath, TempPath, StoragePath);

    /// <summary>
    /// Ensures that the specified directories exist. Creates them if they do not exist.
    /// </summary>
    /// <param name="paths">An array of directory paths to verify and create if necessary.</param>
    private static void EnsureDirectoriesExist(params string[] paths)
    {
        foreach (string path in paths)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to create directory: {path}", ex);
            }
        }
    }
}
