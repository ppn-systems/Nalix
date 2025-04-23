using Nalix.Environment;
using Nalix.Storage.Configurations;
using Nalix.Storage.FileFormats;
using Nalix.Storage.Generator;
using Nalix.Storage.Helpers.MimeTypes;
using Nalix.Storage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nalix.Storage.Local;

/// <summary>
/// Provides an implementation of <see cref="IFileStorage"/> that stores files on disk.
/// </summary>
public class InDiskStorage : IFileStorage
{
    /// <summary>
    /// Configuration for disk-based storage.
    /// </summary>
    private readonly InDiskConfig _storageConfig;

    /// <summary>
    /// Initializes a new instance of <see cref="InDiskStorage"/> with the specified configuration.
    /// </summary>
    /// <param name="storageSettings">The configuration settings for disk storage.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageSettings"/> is null.</exception>
    public InDiskStorage(InDiskConfig storageSettings)
    {
        ArgumentNullException.ThrowIfNull(storageSettings);

        _storageConfig = storageSettings;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InDiskStorage"/> with default settings.
    /// </summary>
    public InDiskStorage() => _storageConfig = new InDiskConfig(Directories.StoragePath)
        .UseFileGenerator(new FileGenerator())
        .UseMimeTypeResolver(new MimeTypeResolver());

    /// <inheritdoc />
    public void Upload(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(metaInfo);

        // Append file extension if missing and MIME type resolver is enabled
        if (!fileName.Contains('.') && _storageConfig.IsMimeTypeResolverEnabled)
        {
            string fileExtension = _storageConfig.MimeTypeResolver.GetExtension(data);
            fileName += fileExtension;
        }

        var filePath = Path.Combine(_storageConfig.StorageLocation, format, fileName);
        var fileInfo = new FileInfo(filePath);

        // Ensure directory exists before writing the file
        if (fileInfo.Directory?.Exists == false)
            fileInfo.Directory.Create();

        File.WriteAllBytes(filePath, data);
    }

    /// <inheritdoc />
    public IFile Download(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var uri = GetFileUri(fileName, format);

        if (string.IsNullOrEmpty(uri))
        {
            if (_storageConfig.IsGenerationEnabled)
            {
                var file = _storageConfig.Generator.Generate(Download(fileName).Data, format);
                return new LocalFile(file.Data, fileName);
            }

            if (!_storageConfig.IsGenerationEnabled && format != Original.FormatName)
                throw new FileNotFoundException($"File {Path.Combine(_storageConfig.StorageLocation, format, fileName)} not found. Plugin in {typeof(IFileGenerator)} to generate it.");

            throw new FileNotFoundException($"File {Path.Combine(_storageConfig.StorageLocation, format, fileName)} not found");
        }

        var fileBytes = File.ReadAllBytes(uri);
        var fileInfo = new FileInfo(uri);

        return new LocalFile(fileBytes, fileInfo.Name);
    }

    /// <inheritdoc />
    public string GetFileUri(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var directoryPath = Path.Combine(_storageConfig.StorageLocation, format);
        var directoryInfo = new DirectoryInfo(directoryPath);
        FileInfo[] files = directoryInfo.GetFiles();

        var found = files.SingleOrDefault(x => x.Name.Equals(fileName, StringComparison.InvariantCultureIgnoreCase));

        return found?.FullName ?? string.Empty;
    }

    /// <inheritdoc />
    public bool FileExists(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var directoryPath = Path.Combine(_storageConfig.StorageLocation, format);
        var directoryInfo = new DirectoryInfo(directoryPath);
        FileInfo[] files = directoryInfo.GetFiles();

        return files.Any(x => x.Name.Equals(fileName, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <inheritdoc />
    public Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Delete(string fileName)
    {
        foreach (var format in _storageConfig.Generator.Formats)
        {
            var uri = GetFileUri(fileName, format.Name);

            if (!string.IsNullOrEmpty(uri))
            {
                File.Delete(uri);
            }
        }
    }
}
