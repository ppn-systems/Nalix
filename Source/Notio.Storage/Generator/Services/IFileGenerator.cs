using Notio.Storage.FileFormats;
using Notio.Storage.Generator.Response;
using System.Collections.Generic;

namespace Notio.Storage.Generator;

public interface IFileGenerator
{
    /// <summary>
    /// Generates a file to specific format
    /// </summary>
    /// <param name="data"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    FileGenerateResponse Generate(byte[] data, string format);

    /// <summary>
    /// Registers Format
    /// </summary>
    /// <param name="format"></param>
    IFileGenerator RegisterFormat(IFileFormat format);

    /// <summary>
    /// All registered formats
    /// </summary>
    IEnumerable<IFileFormat> Formats { get; }
}