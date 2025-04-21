namespace Nalix.Storage.Generator;

/// <summary>
/// Represents the response of a file generation process.
/// </summary>
public class FileGenerateResponse(bool success, byte[] data, string error)
{
    /// <summary>
    /// Gets a value indicating whether the file generation was successful.
    /// </summary>
    public bool Success { get; private set; } = success;

    /// <summary>
    /// Gets the generated file data as a byte array.
    /// </summary>
    public byte[] Data { get; private set; } = data;

    /// <summary>
    /// Gets the error message if the generation failed; otherwise, an empty string.
    /// </summary>
    public string Error { get; private set; } = error;
}