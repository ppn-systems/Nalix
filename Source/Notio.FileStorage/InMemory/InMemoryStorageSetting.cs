using Notio.FileStorage.InSystem;
using Notio.FileStorage.Interfaces;
using Notio.FileStorage.MimeTypes;
using System;

namespace Notio.FileStorage.InMemory;

public class InMemoryStorageSetting : IFileStorageSetting<InMemoryStorageSetting>
{
    public IFileGenerator Generator { get; private set; }
    public IMimeTypeResolver MimeTypeResolver { get; private set; }

    public bool IsGenerationEnabled
    {
        get
        {
            return Generator is null == false;
        }
    }

    public bool IsMimeTypeResolverEnabled
    {
        get
        {
            return MimeTypeResolver is null == false;
        }
    }

    public InMemoryStorageSetting UseFileGenerator(IFileGenerator generator)
    {
        if (generator is null == true) throw new ArgumentNullException(nameof(generator));
        Generator = generator;
        return this;
    }

    public InMemoryStorageSetting UseMimeTypeResolver(IMimeTypeResolver resolver)
    {
        if (resolver is null == true) throw new ArgumentNullException(nameof(resolver));
        MimeTypeResolver = resolver;
        return this;
    }
}