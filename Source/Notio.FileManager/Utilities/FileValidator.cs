using System;
using System.IO;

namespace Notio.FileManager.Utilities;

public static class FileValidator
{
    public static bool IsFileSizeValid(string filePath, long maxSizeInBytes = 10 * 1024 * 1024)
    {
        if (!PathHelper.IsFile(filePath))
            return false;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length <= maxSizeInBytes;
    }

    public static bool IsFileExtensionValid(string filePath, string[] allowedExtensions)
    {
        if (!PathHelper.IsFile(filePath))
            return false;

        string fileExtension = Path.GetExtension(filePath).ToLower();
        foreach (string extension in allowedExtensions)
        {
            if (fileExtension.Equals(extension, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool IsFileReadable(string filePath)
    {
        if (!PathHelper.IsFile(filePath))
            return false;

        try
        {
            using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsFileWritable(string filePath)
    {
        if (!PathHelper.IsFile(filePath))
            return false;

        try
        {
            using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Write);
            return true;
        }
        catch
        {
            return false;
        }
    }
}