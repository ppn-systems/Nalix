using System.Collections.Generic;

namespace Notio.FileStorage.MimeTypes;

public interface IMimeTypeResolver
{
    string DefaultMimeType { get; }

    IReadOnlyCollection<string> SupportedTypes { get; }

    string GetMimeType(byte[] data);

    string GetExtension(byte[] data);
}