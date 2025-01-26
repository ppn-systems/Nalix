using System.Collections.Generic;

namespace Notio.FileStorage.MimeTypes;

/// <summary>
/// Defines an interface for resolving MIME types and file extensions based on file content.
/// </summary>
public interface IMimeTypeResolver
{
    /// <summary>
    /// Gets the default MIME type used when no specific type is detected.
    /// </summary>
    string DefaultMimeType { get; }

    /// <summary>
    /// Gets the collection of supported MIME types.
    /// </summary>
    IReadOnlyCollection<string> SupportedTypes { get; }

    /// <summary>
    /// Determines the MIME type of the provided data.
    /// </summary>
    /// <param name="data">The byte array representing the file content.</param>
    /// <returns>A string representing the MIME type.</returns>
    string GetMimeType(byte[] data);

    /// <summary>
    /// Determines the file extension based on the provided data.
    /// </summary>
    /// <param name="data">The byte array representing the file content.</param>
    /// <returns>A string representing the file extension, including the dot (e.g., ".txt").</returns>
    string GetExtension(byte[] data);

    /// <summary>
    /// Checks if a specific MIME type is supported.
    /// </summary>
    /// <param name="mimeType">The MIME type to check.</param>
    /// <returns><c>true</c> if the MIME type is supported; otherwise, <c>false</c>.</returns>
    bool IsSupported(string mimeType);
}