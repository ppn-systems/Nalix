namespace Notio.Storage.Models;

/// <summary>
/// Represents a file with data and a name.
/// </summary>
public interface IFile
{
    /// <summary>
    /// Gets or sets the file's binary data.
    /// </summary>
    byte[] Data { get; set; }

    /// <summary>
    /// Gets or sets the file's name, including extension.
    /// </summary>
    string Name { get; set; }
}