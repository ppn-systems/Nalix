using Notio.Storage.FileFormats;
using System;
using System.Collections.Generic;

namespace Notio.Storage.Generator;

/// <summary>
/// File generator class that supports multiple file formats.
/// </summary>
public class FileGenerator : IFileGenerator
{
    private readonly Dictionary<string, IFileFormat> _formats;

    /// <summary>
    /// Gets all registered file formats.
    /// </summary>
    public IEnumerable<IFileFormat> Formats => _formats.Values;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileGenerator"/> class with the default format.
    /// </summary>
    public FileGenerator()
    {
        _formats = [];
        RegisterFormat(new Original());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileGenerator"/> class with additional formats.
    /// </summary>
    /// <param name="formats">A collection of file formats to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="formats"/> is null.</exception>
    public FileGenerator(IEnumerable<IFileFormat> formats) : this()
    {
        ArgumentNullException.ThrowIfNull(formats);

        foreach (var format in formats)
        {
            if (!_formats.ContainsKey(format.Name))
                _formats.Add(format.Name, format);
        }
    }

    /// <summary>
    /// Generates a file in the specified format.
    /// </summary>
    /// <param name="data">The input data as a byte array.</param>
    /// <param name="format">The target file format.</param>
    /// <returns>A <see cref="FileGenerateResponse"/> containing the result of the generation process.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown if the specified format is not supported.</exception>
    public FileGenerateResponse Generate(byte[] data, string format)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!_formats.TryGetValue(format, out IFileFormat? formatInstance))
            throw new NotSupportedException($"This file format is not supported: {format}");

        return formatInstance.Generate(data);
    }

    /// <summary>
    /// Registers a new file format.
    /// </summary>
    /// <param name="format">The file format to register.</param>
    /// <returns>The current instance of <see cref="FileGenerator"/> for chaining.</returns>
    public IFileGenerator RegisterFormat(IFileFormat format)
    {
        _formats.TryAdd(format.Name, format);
        return this;
    }
}
