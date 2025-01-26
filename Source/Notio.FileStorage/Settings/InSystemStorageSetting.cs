using Notio.FileStorage.Interfaces;
using Notio.FileStorage.MimeTypes;
using System;
using System.IO;

namespace Notio.FileStorage.Settings;

public class InSystemStorageSetting : IFileStorageSetting<InSystemStorageSetting>
{
    public string StorageFolder { get; private set; }
    public IFileGenerator Generator { get; private set; }

    public bool IsGenerationEnabled
    {
        get
        {
            return Generator is null == false;
        }
    }

    public IMimeTypeResolver MimeTypeResolver { get; private set; }

    public bool IsMimeTypeResolverEnabled
    {
        get
        {
            return MimeTypeResolver is null == false;
        }
    }

    public InSystemStorageSetting(string storageFolder)
    {
        if (Directory.Exists(storageFolder) == false)
            Directory.CreateDirectory(storageFolder);

        StorageFolder = storageFolder;
    }

    public InSystemStorageSetting UseFileGenerator(IFileGenerator generator)
    {
        if (generator is null == true) throw new ArgumentNullException(nameof(generator));
        Generator = generator;
        return this;
    }

    public InSystemStorageSetting UseMimeTypeResolver(IMimeTypeResolver resolver)
    {
        if (resolver is null == true) throw new ArgumentNullException(nameof(resolver));
        MimeTypeResolver = resolver;
        return this;
    }
}