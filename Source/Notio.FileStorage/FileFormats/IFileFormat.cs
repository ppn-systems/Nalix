using Notio.FileStorage.Generator;

namespace Notio.FileStorage.FileFormats;

public interface IFileFormat
{
    string Name { get; }

    FileGenerateResponse Generate(byte[] data);
}