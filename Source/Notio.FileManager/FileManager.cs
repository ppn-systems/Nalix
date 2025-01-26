using Notio.FileManager.Services;

namespace Notio.FileManager;

public static class FileManager
{
    public static void ConvertFileToNotio(string filePath, string user)
        => FileConverter.ConvertToNotio(filePath, user);

    public static void RestoreFileFromNotio(string notioFilePath)
        => FileConverter.RestoreFromNotio(notioFilePath);
}