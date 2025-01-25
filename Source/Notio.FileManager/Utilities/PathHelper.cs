using System;
using System.IO;

namespace Notio.FileManager.Utilities;

public static class PathHelper
{
    public static string CombinePaths(string basePath, string relativePath)
        => Path.Combine(basePath, relativePath);

    public static bool IsValidPath(string path)
    {
        try
        {
            Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string GetFileName(string path)
        => Path.GetFileName(path);

    public static string GetParentDirectory(string path) => Path.GetDirectoryName(path);

    public static bool IsDirectory(string path) => Directory.Exists(path);

    public static bool IsFile(string path) => File.Exists(path);
}