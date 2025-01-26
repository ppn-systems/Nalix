using Notio.FileStorage.Interfaces;

namespace Notio.FileStorage.Models;

/// <summary>
/// Represents a local file with its data and file name.
/// </summary>
public class LocalFile(byte[] data, string fileName) : IFile
{
    /// <inheritdoc />
    public byte[] Data { get; set; } = data;

    /// <inheritdoc />
    public string Name { get; set; } = fileName;
}