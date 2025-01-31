using Notio.Storage.Generator.Response;

namespace Notio.Storage.FileFormats;

/// <summary>
/// Represents a file format that supports generating a file response from byte data.
/// </summary>
public interface IFileFormat
{
    /// <summary>
    /// Gets the name of the file format.
    /// </summary>
    /// <value>The name of the file format.</value>
    string Name { get; }

    /// <summary>
    /// Generates a file response based on the provided byte data.
    /// </summary>
    /// <param name="data">The byte data representing the file content.</param>
    /// <returns>A <see cref="FileGenerateResponse"/> containing the generated file response.</returns>
    FileGenerateResponse Generate(byte[] data);
}