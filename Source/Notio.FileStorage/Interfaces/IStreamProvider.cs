using Notio.FileStorage.Models;
using System.Collections.Generic;
using System.IO;

namespace Notio.FileStorage.Interfaces;

public interface IStreamProvider
{
    /// <summary>
    /// Retrieves a stream for the specified file.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="metaInfo">The metadata associated with the file.</param>
    /// <param name="format">The format of the file (default is "original").</param>
    /// <returns>A <see cref="Stream"/> representing the file's content.</returns>
    Stream GetStream(string fileName, IEnumerable<FileMeta> metaInfo, string format = "original");
}