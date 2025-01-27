using Notio.FileStorage.Config;
using Notio.FileStorage.FileFormats;
using Notio.FileStorage.Generator;
using Notio.FileStorage.Interfaces;
using Notio.FileStorage.MimeTypes;
using Notio.FileStorage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Notio.FileStorage.Local;

public class InMemoryStorage : IFileStorageAsync
{
    private readonly InMemoryConfig _storageConfig;
    private readonly Dictionary<string, InMemoryFile> _storage = [];

    public InMemoryStorage(InMemoryConfig storageConfig)
    {
        ArgumentNullException.ThrowIfNull(storageConfig);
        this._storageConfig = storageConfig;
    }

    public InMemoryStorage() => _storageConfig = new InMemoryConfig()
        .UseFileGenerator(new FileGenerator())
        .UseMimeTypeResolver(new MimeTypeResolver());

    public async Task<IFile> DownloadAsync(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        var uri = await GetFileUriAsync(fileName, format).ConfigureAwait(false);
        var key = GetKey(fileName, format);

        if (string.IsNullOrEmpty(uri))
        {
            if (_storageConfig.IsGenerationEnabled == true)
            {
                var downloadResult = await DownloadAsync(fileName).ConfigureAwait(false);
                var file = _storageConfig.Generator.Generate(downloadResult.Data, format);
                return new LocalFile(file.Data, fileName);
            }

            if (_storageConfig.IsGenerationEnabled == false && format != Original.FormatName)
                throw new FileNotFoundException($"File {key} not found. Plugin in {typeof(IFileGenerator)} to generate it.");

            throw new FileNotFoundException($"File {key} not found");
        }

        var fileBytes = _storage[key].Data;

        return new LocalFile(fileBytes, fileName);
    }

    public Task<bool> FileExistsAsync(string fileName, string format = "original")
    {
        var key = GetKey(fileName, format);
        return Task.FromResult(_storage.ContainsKey(key));
    }

    public Task<string> GetFileUriAsync(string fileName, string format = "original")
    {
        var key = GetKey(fileName, format);
        return Task.FromResult(_storage.ContainsKey(key) ? key : string.Empty);
    }

    public Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        throw new NotImplementedException();
    }

    public Task UploadAsync(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(metaInfo);
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        var key = GetKey(fileName, format);

        var metaDictionary = new Dictionary<string, string>();
        foreach (var meta in metaInfo)
            metaDictionary.Add(Uri.EscapeDataString(meta.Key), Uri.EscapeDataString(meta.Value));

        string contentType = _storageConfig.MimeTypeResolver.GetMimeType(data);

        var file = new InMemoryFile(data, metaDictionary, contentType);

        if (!_storage.TryAdd(key, file))
            _storage[key] = file;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string fileName)
    {
        foreach (var format in _storageConfig.Generator.Formats)
        {
            var key = GetKey(fileName, format.Name);

            _storage.Remove(key);
        }

        return Task.CompletedTask;
    }

    private static string GetKey(string fileName, string format) => format + "/" + fileName;

    private class InMemoryFile(byte[] data, Dictionary<string, string> metaInfo, string contentType)
    {
        public byte[] Data { get; private set; } = data;

        public Dictionary<string, string> MetaInfo { get; private set; } = metaInfo;

        public string ContentType { get; private set; } = contentType;
    }
}