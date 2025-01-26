using System;
using System.IO;
using Notio.FileManager.Models;

namespace Notio.FileManager.Services
{
    public static class FileConverter
    {
        public static void ConvertToNotio(string filePath, string user)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            string originalExtension = Path.GetExtension(filePath);
            string notioFilePath = Path.ChangeExtension(filePath, ".notio");

            var metadata = new FileMetadata
            {
                OriginalExtension = originalExtension,
                User = user,
                CreatedDate = DateTime.Now
            };

            string originalContent = File.ReadAllText(filePath);
            FileMetadataService.WriteMetadata(notioFilePath, metadata, originalContent);
            File.Delete(filePath);
        }

        public static void RestoreFromNotio(string notioFilePath)
        {
            if (!File.Exists(notioFilePath))
                throw new FileNotFoundException("File not found.", notioFilePath);
            var (metadata, originalContent) = FileMetadataService.ReadMetadata(notioFilePath);
            string originalFilePath = Path.ChangeExtension(notioFilePath, metadata.OriginalExtension);
            File.WriteAllText(originalFilePath, originalContent);
            File.Delete(notioFilePath);
        }
    }
}