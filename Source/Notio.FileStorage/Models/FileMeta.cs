namespace Notio.FileStorage.Models;

/// <summary>
/// Represents metadata associated with a file.
/// </summary>
public class FileMeta(string key, string value)
{
    /// <summary>
    /// Gets the key of the metadata.
    /// </summary>
    public string Key { get; private set; } = key;

    /// <summary>
    /// Gets the value of the metadata.
    /// </summary>
    public string Value { get; private set; } = value;
}