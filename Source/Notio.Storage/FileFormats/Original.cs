using Notio.Storage.Generator;

namespace Notio.Storage.FileFormats;

/// <summary>
/// Represents the original file format.
/// </summary>
public class Original : IFileFormat
{
    /// <summary>
    /// The name of the file format, which is the name of the class in lowercase.
    /// </summary>
    public static readonly string FormatName = typeof(Original).Name.ToLowerInvariant();

    /// <summary>
    /// Gets the name of the file format.
    /// </summary>
    /// <value>The name of the file format.</value>
    public string Name => FormatName;

    /// <summary>
    /// Generates a file response from the given byte data.
    /// </summary>
    /// <param name="data">The byte array representing the file content.</param>
    /// <returns>A <see cref="FileGenerateResponse"/> containing the generation status, file data, and any error message.</returns>
    public FileGenerateResponse Generate(byte[] data) => new(true, data, string.Empty);
}