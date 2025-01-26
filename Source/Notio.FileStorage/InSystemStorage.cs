using Notio.FileStorage.FileFormats;
using Notio.FileStorage.Interfaces;
using Notio.FileStorage.Models;
using Notio.FileStorage.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Notio.FileStorage;

public class InSystemStorage : IFileStorage
{
    private readonly InSystemStorageSetting storageSettings;

    public InSystemStorage(InSystemStorageSetting storageSettings)
    {
        if (storageSettings is null == true) throw new ArgumentNullException(nameof(storageSettings));
        this.storageSettings = storageSettings;
    }

    public void Upload(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        if (data is null == true)
            throw new ArgumentNullException(nameof(data));

        if (metaInfo is null == true)
            throw new ArgumentNullException(nameof(metaInfo));

        if (fileName.Contains('.') == false && storageSettings.IsMimeTypeResolverEnabled)
        {
            var fileExtension = storageSettings.MimeTypeResolver.GetExtension(data);
            fileName += fileExtension;
        }

        var filePath = Path.Combine(storageSettings.StorageFolder, format, fileName);
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Directory.Exists)
            fileInfo.Directory.Create();

        File.WriteAllBytes(filePath, data);
    }

    public IFile Download(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        var uri = GetFileUri(fileName, format);

        if (string.IsNullOrEmpty(uri))
        {
            if (storageSettings.IsGenerationEnabled == true)
            {
                var file = storageSettings.Generator.Generate(Download(fileName).Data, format);
                return new LocalFile(file.Data, fileName);
            }

            if (storageSettings.IsGenerationEnabled == false && format != Original.FormatName)
                throw new FileNotFoundException($"File {Path.Combine(storageSettings.StorageFolder, format, fileName)} not found. Plugin in {typeof(IFileGenerator)} to generate it.");

            throw new FileNotFoundException($"File {Path.Combine(storageSettings.StorageFolder, format, fileName)} not found");
        }

        var fileBytes = File.ReadAllBytes(uri);

        var fileInfo = new FileInfo(uri);

        return new LocalFile(fileBytes, fileInfo.Name);
    }

    public string GetFileUri(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        var directoryPath = Path.Combine(storageSettings.StorageFolder, format);
        var directoryInfo = new DirectoryInfo(directoryPath);
        FileInfo[] files = directoryInfo.GetFiles();
        var found = files.SingleOrDefault(x => x.Name.Equals(fileName, StringComparison.InvariantCultureIgnoreCase));

        if (found is null == true)
            return string.Empty;

        return found.FullName;
    }

    public bool FileExists(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var directoryPath = Path.Combine(storageSettings.StorageFolder, format);
        var directoryInfo = new DirectoryInfo(directoryPath);
        FileInfo[] files = directoryInfo.GetFiles();
        var found = files.SingleOrDefault(x => x.Name.Equals(fileName, StringComparison.InvariantCultureIgnoreCase));

        return found != null;
    }

    public Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        throw new NotImplementedException();
    }

    public void Delete(string fileName)
    {
        foreach (var format in storageSettings.Generator.Formats)
        {
            var uri = GetFileUri(fileName, format.Name);

            if (string.IsNullOrEmpty(uri) == false)
            {
                File.Delete(uri);
            }
        }
    }
}