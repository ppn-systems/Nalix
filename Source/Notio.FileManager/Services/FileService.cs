using System;
using System.IO;
using Notio.FileManager.Utilities;

namespace Notio.FileManager.Services;

public static class FileService
{
    // Đọc file dưới dạng byte
    public static byte[] ReadFileAsBytes(string filePath)
    {
        if (!PathHelper.IsFile(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        if (!FileValidator.IsFileReadable(filePath))
            throw new UnauthorizedAccessException("File is not readable.");

        return File.ReadAllBytes(filePath);
    }

    // Ghi file dưới dạng byte
    public static void WriteFileAsBytes(string filePath, byte[] data)
    {
        if (!FileValidator.IsFileWritable(filePath))
            throw new UnauthorizedAccessException("File is not writable.");

        File.WriteAllBytes(filePath, data);
    }

    public static void AppendToFile(string filePath, byte[] data)
    {
        if (!PathHelper.IsFile(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        if (!FileValidator.IsFileWritable(filePath))
            throw new UnauthorizedAccessException("File is not writable.");

        using FileStream fileStream = new(filePath, FileMode.Append, FileAccess.Write);
        fileStream.Write(data, 0, data.Length);
    }

    // Xóa file
    public static void DeleteFile(string filePath)
    {
        if (!PathHelper.IsFile(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        File.Delete(filePath);
    }

    // Sao chép file
    public static void CopyFile(string sourceFilePath, string destinationFilePath)
    {
        if (!PathHelper.IsFile(sourceFilePath))
            throw new FileNotFoundException("Source file not found.", sourceFilePath);

        if (PathHelper.IsFile(destinationFilePath))
            throw new IOException("Destination file already exists.");

        File.Copy(sourceFilePath, destinationFilePath);
    }

    // Di chuyển file
    public static void MoveFile(string sourceFilePath, string destinationFilePath)
    {
        if (!PathHelper.IsFile(sourceFilePath))
            throw new FileNotFoundException("Source file not found.", sourceFilePath);

        if (PathHelper.IsFile(destinationFilePath))
            throw new IOException("Destination file already exists.");

        File.Move(sourceFilePath, destinationFilePath);
    }
}