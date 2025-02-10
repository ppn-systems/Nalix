using Notio.Storage.FileFormats;
using System.Collections.Generic;

namespace Notio.Storage.Generator;

/// <summary>
/// Defines an interface for file format generation and transformation.
/// </summary>
public interface IFileGenerator
{
    /// <summary>
    /// Generates a file in the specified format.
    /// </summary>
    /// <param name="data">The raw byte data of the file.</param>
    /// <param name="format">The target format to generate.</param>
    /// <returns>A <see cref="FileGenerateResponse"/> containing the generated file data and metadata.</returns>
    FileGenerateResponse Generate(byte[] data, string format);

    /// <summary>
    /// Registers a new file format to the generator.
    /// </summary>
    /// <param name="format">The <see cref="IFileFormat"/> to register.</param>
    /// <returns>The current instance of <see cref="IFileGenerator"/> to allow method chaining.</returns>
    IFileGenerator RegisterFormat(IFileFormat format);

    /// <summary>
    /// Gets all registered file formats.
    /// </summary>
    IEnumerable<IFileFormat> Formats { get; }
}
