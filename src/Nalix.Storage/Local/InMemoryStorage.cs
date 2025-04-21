using Nalix.Storage.Configurations;
using Nalix.Storage.FileFormats;
using Nalix.Storage.Generator;
using Nalix.Storage.Helpers.MimeTypes;
using Nalix.Storage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Nalix.Storage.Local;

/// <summary>
/// Provides an in-memory implementation of <see cref="IFileStorageAsync"/> for storing and retrieving files.
/// </summary>
public class InMemoryStorage : IFileStorageAsync
{
    /// <summary>
    /// Configuration for the in-memory storage.
    /// </summary>
    private readonly InMemoryConfig _storageConfig;

    /// <summary>
    /// Dictionary to store files in memory, using a key generated from file name and format.
    /// </summary>
    private readonly Dictionary<string, InMemoryFile> _storage = [];

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryStorage"/> with the specified storage configuration.
    /// </summary>
    /// <param name="storageConfig">The configuration settings for in-memory storage.</param>
    public InMemoryStorage(InMemoryConfig storageConfig)
    {
        ArgumentNullException.ThrowIfNull(storageConfig);
        _storageConfig = storageConfig;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryStorage"/> with default settings.
    /// </summary>
    public InMemoryStorage() => _storageConfig = new InMemoryConfig(new FileGenerator())
        .UseFileGenerator(new FileGenerator())
        .UseMimeTypeResolver(new MimeTypeResolver());

    /// <inheritdoc />
    public async Task<IFile> DownloadAsync(string fileName, string format = "original")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var uri = await GetFileUriAsync(fileName, format).ConfigureAwait(false);
        var key = GetKey(fileName, format);

        if (string.IsNullOrEmpty(uri))
        {
            if (_storageConfig.IsGenerationEnabled)
            {
                var downloadResult = await DownloadAsync(fileName).ConfigureAwait(false);
                var file = _storageConfig.Generator.Generate(downloadResult.Data, format);
                return new LocalFile(file.Data, fileName);
            }

            if (!_storageConfig.IsGenerationEnabled && format != Original.FormatName)
                throw new FileNotFoundException($"File {key} not found. Plugin in {typeof(IFileGenerator)} to generate it.");

            throw new FileNotFoundException($"File {key} not found.");
        }

        var fileBytes = _storage[key].Data;
        return new LocalFile(fileBytes, fileName);
    }

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(string fileName, string format = "original")
    {
        var key = GetKey(fileName, format);
        return Task.FromResult(_storage.ContainsKey(key));
    }

    /// <inheritdoc />
    public Task<string> GetFileUriAsync(string fileName, string format = "original")
    {
        var key = GetKey(fileName, format);
        return Task.FromResult(_storage.ContainsKey(key) ? key : string.Empty);
    }

    /// <inheritdoc />
    public Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task UploadAsync(string fileName, byte[] data, IEnumerable<FileMeta> metaInfo, string format = "original")
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(metaInfo);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        var key = GetKey(fileName, format);

        var metaDictionary = new Dictionary<string, string>();
        foreach (var meta in metaInfo)
            metaDictionary.Add(Uri.EscapeDataString(meta.Key), Uri.EscapeDataString(meta.Value));

        string contentType = _storageConfig.MimeTypeResolver?.GetMimeType(data) ?? string.Empty;

        var file = new InMemoryFile(data, metaDictionary, contentType);

        if (!_storage.TryAdd(key, file))
            _storage[key] = file;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string fileName)
    {
        foreach (var format in _storageConfig.Generator.Formats)
        {
            var key = GetKey(fileName, format.Name);
            _storage.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a unique key based on the file name and format.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="format">The format of the file.</param>
    /// <returns>A string key used to identify the file in storage.</returns>
    private static string GetKey(string fileName, string format) => $"{format}/{fileName}";

    /// <summary>
    /// Represents an in-memory file with its associated metadata.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of <see cref="InMemoryFile"/>.
    /// </remarks>
    /// <param name="data">The file data as a byte array.</param>
    /// <param name="metaInfo">Metadata associated with the file.</param>
    /// <param name="contentType">The MIME type of the file.</param>
    private class InMemoryFile(byte[] data, Dictionary<string, string> metaInfo, string contentType)
    {
        /// <summary>
        /// Gets the file data.
        /// </summary>
        public byte[] Data { get; } = data;

        /// <summary>
        /// Gets the metadata associated with the file.
        /// </summary>
        public Dictionary<string, string> MetaInfo { get; } = metaInfo;

        /// <summary>
        /// Gets the MIME type of the file.
        /// </summary>
        public string ContentType { get; } = contentType;
    }
}
