using System;

namespace Notio.Storage.Helpers.MimeTypes;

/// <summary>
/// Represents a MIME type mapping with its name, extension, MIME type, and pattern.
/// </summary>
internal class MimeTypeMapper
{
    /// <summary>
    /// Gets the name of the MIME type.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the MIME type pattern used to detect the file format.
    /// </summary>
    public MimeTypePattern Pattern { get; private set; }

    /// <summary>
    /// Gets the MIME type.
    /// </summary>
    public string Mime { get; private set; }

    /// <summary>
    /// Gets the file extension associated with the MIME type.
    /// </summary>
    public string Extension { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimeTypeMapper"/> class with the provided details.
    /// </summary>
    /// <param name="name">The name of the MIME type.</param>
    /// <param name="extension">The file extension associated with the MIME type.</param>
    /// <param name="mime">The MIME type string.</param>
    /// <param name="pattern">The MIME type pattern to match against file data.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null or empty.</exception>
    public MimeTypeMapper(string name, string extension, string mime, MimeTypePattern pattern)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name), "Name cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentNullException(nameof(extension), "Extension cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(mime)) throw new ArgumentNullException(nameof(mime), "MIME type cannot be null or empty.");

        Name = name;
        Extension = extension;
        Mime = mime;
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern), "Pattern cannot be null.");
    }
}