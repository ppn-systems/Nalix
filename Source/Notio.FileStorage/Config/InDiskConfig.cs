using Notio.FileStorage.Interfaces;
using Notio.FileStorage.MimeTypes;
using System;
using System.IO;

namespace Notio.FileStorage.Config;

public class InDiskConfig : IFileStorageConfig<InDiskConfig>
{
    public string StorageFolder { get; }
    public IFileGenerator Generator { get; private set; }
    public IMimeTypeResolver MimeTypeResolver { get; private set; }

    public bool IsGenerationEnabled => Generator != null;
    public bool IsMimeTypeResolverEnabled => MimeTypeResolver != null;

    public InDiskConfig(string storageFolder)
    {
        StorageFolder = storageFolder ?? throw new ArgumentNullException(nameof(storageFolder));
        if (!Directory.Exists(StorageFolder))
            Directory.CreateDirectory(StorageFolder);
    }

    public InDiskConfig UseFileGenerator(IFileGenerator generator)
    {
        Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        return this;
    }

    public InDiskConfig UseMimeTypeResolver(IMimeTypeResolver resolver)
    {
        MimeTypeResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }
}