using Notio.FileStorage.FileFormats;
using Notio.FileStorage.Interfaces;
using Notio.FileStorage.Models;
using Notio.FileStorage.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Notio.FileStorage.Local;

public class InMemoryStorage : IFileStorageAsync
{
    private readonly Dictionary<string, InMemoryFile> storage = [];
    private readonly InMemoryStorageSetting storageSettings;

    public InMemoryStorage(InMemoryStorageSetting storageSettings)
    {
        ArgumentNullException.ThrowIfNull(storageSettings);
        this.storageSettings = storageSettings;
    }

    public async Task<IFile> DownloadAsync(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        var uri = await GetFileUriAsync(fileName, format).ConfigureAwait(false);
        var key = GetKey(fileName, format);

        if (string.IsNullOrEmpty(uri))
        {
            if (storageSettings.IsGenerationEnabled == true)
            {
                var downloadResult = await DownloadAsync(fileName).ConfigureAwait(false);
                var file = storageSettings.Generator.Generate(downloadResult.Data, format);
                return new LocalFile(file.Data, fileName);
            }

            if (storageSettings.IsGenerationEnabled == false && format != Original.FormatName)
                throw new FileNotFoundException($"File {key} not found. Plugin in {typeof(IFileGenerator)} to generate it.");

            throw new FileNotFoundException($"File {key} not found");
        }

        var fileBytes = storage[key].Data;

        return new LocalFile(fileBytes, fileName);
    }

    public Task<bool> FileExistsAsync(string fileName, string format = "original")
    {
        var key = GetKey(fileName, format);
        return Task.FromResult(storage.ContainsKey(key));
    }

    public Task<string> GetFileUriAsync(string fileName, string format = "original")
    {
        var key = GetKey(fileName, format);
        return Task.FromResult(storage.ContainsKey(key) ? key : string.Empty);
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

        string contentType = storageSettings.MimeTypeResolver.GetMimeType(data);

        var file = new InMemoryFile(data, metaDictionary, contentType);

        if (!storage.TryAdd(key, file))
            storage[key] = file;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string fileName)
    {
        foreach (var format in storageSettings.Generator.Formats)
        {
            var key = GetKey(fileName, format.Name);

            storage.Remove(key);
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