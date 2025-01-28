using System;
using System.IO;

namespace Notio.Shared;

/// <summary>
/// Class that defines default directories for the application.
/// </summary>
public static class DefaultDirectories
{
    private static readonly Lazy<string> _basePath = new(() => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
    private static readonly Lazy<string> _logsPath = new(() => Path.Combine(BasePath, "Logs"));
    private static readonly Lazy<string> _dataPath = new(() => Path.Combine(BasePath, "Data"));
    private static readonly Lazy<string> _tempPath = new(() => Path.Combine(DataPath, "Temp"));
    private static readonly Lazy<string> _configPath = new(() => Path.Combine(BasePath, "Config"));
    private static readonly Lazy<string> _storagePath = new(() => Path.Combine(DataPath, "Storage"));

    /// <summary>
    /// The base directory of the application.
    /// </summary>
    public static string BasePath => _basePath.Value;

    /// <summary>
    /// Directory for storing log files.
    /// </summary>
    public static string LogsPath => _logsPath.Value;

    /// <summary>
    /// Directory for storing application data files.
    /// </summary>
    public static string DataPath => _dataPath.Value;

    /// <summary>
    /// Directory for storing system configuration files.
    /// </summary>
    public static string ConfigPath => _configPath.Value;

    /// <summary>
    /// Directory for storing temporary files.
    /// </summary>
    public static string TempPath => _tempPath.Value;

    /// <summary>
    /// Directory for storing metric-related files.
    /// </summary>
    public static string StoragePath => _storagePath.Value;

    /// <summary>
    /// Static constructor to initialize the default directories.
    /// Ensures that all necessary directories are created.
    /// </summary>
    static DefaultDirectories()
    {
        EnsureDirectoriesExist(
            LogsPath, DataPath,
            ConfigPath, TempPath,
            StoragePath
        );
    }

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