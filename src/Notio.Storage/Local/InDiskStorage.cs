using Notio.Shared;
using Notio.Storage.Config;
using Notio.Storage.FileFormats;
using Notio.Storage.Generator;
using Notio.Storage.Generator.Services;
using Notio.Storage.MimeTypes;
using Notio.Storage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Notio.Storage.Local;

public class InDiskStorage : IFileStorage
{
    private readonly InDiskConfig _storageConfig;

    public InDiskStorage(InDiskConfig storageSettings)
    {
        if (storageSettings is null == true) throw new ArgumentNullException(nameof(storageSettings));
        this._storageConfig = storageSettings;
    }

    public InDiskStorage() => _storageConfig = new InDiskConfig(DefaultDirectories.StoragePath)
        .UseFileGenerator(new FileGenerator())
        .UseMimeTypeResolver(new MimeTypeResolver());

    public void Upload(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        if (data is null == true)
            throw new ArgumentNullException(nameof(data));

        if (metaInfo is null == true)
            throw new ArgumentNullException(nameof(metaInfo));

        if (fileName.Contains('.') == false && _storageConfig.IsMimeTypeResolverEnabled)
        {
            string fileExtension = _storageConfig.MimeTypeResolver.GetExtension(data);
            fileName += fileExtension;
        }

        var filePath = Path.Combine(_storageConfig.StorageLocation, format, fileName);
        var fileInfo = new FileInfo(filePath);

        if (fileInfo.Directory?.Exists == false)
            fileInfo.Directory.Create();

        File.WriteAllBytes(filePath, data);
    }

    public IFile Download(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        var uri = GetFileUri(fileName, format);

        if (string.IsNullOrEmpty(uri))
        {
            if (_storageConfig.IsGenerationEnabled == true)
            {
                var file = _storageConfig.Generator.Generate(Download(fileName).Data, format);
                return new LocalFile(file.Data, fileName);
            }

            if (_storageConfig.IsGenerationEnabled == false && format != Original.FormatName)
                throw new FileNotFoundException($"File {Path.Combine(_storageConfig.StorageLocation, format, fileName)} not found. Plugin in {typeof(IFileGenerator)} to generate it.");

            throw new FileNotFoundException($"File {Path.Combine(_storageConfig.StorageLocation, format, fileName)} not found");
        }

        var fileBytes = File.ReadAllBytes(uri);

        var fileInfo = new FileInfo(uri);

        return new LocalFile(fileBytes, fileInfo.Name);
    }

    public string GetFileUri(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        var directoryPath = Path.Combine(_storageConfig.StorageLocation, format);
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

        var directoryPath = Path.Combine(_storageConfig.StorageLocation, format);
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
        foreach (var format in _storageConfig.Generator.Formats)
        {
            var uri = GetFileUri(fileName, format.Name);

            if (string.IsNullOrEmpty(uri) == false)
            {
                File.Delete(uri);
            }
        }
    }
}