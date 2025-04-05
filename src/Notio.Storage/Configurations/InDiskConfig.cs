using Notio.Common.Attributes;
using Notio.Shared.Configuration;
using Notio.Storage.Generator;
using Notio.Storage.Helpers.MimeTypes;
using Notio.Storage.Models;
using System;
using System.IO;

namespace Notio.Storage.Configurations;

/// <summary>
/// Configured for file storage on disk.
/// </summary>
public class InDiskConfig : ConfigurationBinder, IFileStorageConfig<InDiskConfig>
{
    /// <summary>
    /// Gets the location of the storage folder on disk.
    /// </summary>
    public string StorageLocation { get; }

    /// <summary>
    /// Gets the file generator instance for generating files.
    /// </summary>
    [ConfiguredIgnore]
    public IFileGenerator Generator { get; private set; } = null!;

    /// <summary>
    /// Gets the MIME type resolver instance.
    /// </summary>
    [ConfiguredIgnore]
    public IMimeTypeResolver MimeTypeResolver { get; private set; } = null!;

    /// <summary>
    /// Indicates whether file generation is enabled.
    /// </summary>
    [ConfiguredIgnore]
    public bool IsGenerationEnabled => Generator != null;

    /// <summary>
    /// Indicates whether the MIME type resolver is enabled.
    /// </summary>
    [ConfiguredIgnore]
    public bool IsMimeTypeResolverEnabled => MimeTypeResolver != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="InDiskConfig"/> class.
    /// Ensures that the specified storage folder exists.
    /// </summary>
    /// <param name="storageLocation">The folder path for storing files.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageLocation"/> is null.</exception>
    public InDiskConfig(string storageLocation)
    {
        StorageLocation = storageLocation ?? throw new ArgumentNullException(nameof(storageLocation));
        if (!Directory.Exists(StorageLocation))
            Directory.CreateDirectory(StorageLocation);
    }

    /// <summary>
    /// Configures the file generator to use for this storage configuration.
    /// </summary>
    /// <param name="generator">The file generator to use.</param>
    /// <returns>The updated <see cref="InDiskConfig"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="generator"/> is null.</exception>
    public InDiskConfig UseFileGenerator(IFileGenerator generator)
    {
        Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        return this;
    }

    /// <summary>
    /// Configures the MIME type resolver to use for this storage configuration.
    /// </summary>
    /// <param name="resolver">The MIME type resolver to use.</param>
    /// <returns>The updated <see cref="InDiskConfig"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="resolver"/> is null.</exception>
    public InDiskConfig UseMimeTypeResolver(IMimeTypeResolver resolver)
    {
        MimeTypeResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }
}
