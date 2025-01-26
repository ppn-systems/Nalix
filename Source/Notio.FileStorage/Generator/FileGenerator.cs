using System;
using System.Collections.Generic;
using Notio.FileStorage.FileFormats;
using Notio.FileStorage.Interfaces;

namespace Notio.FileStorage.Generator;

public class FileGenerator : IFileGenerator
{
    private readonly Dictionary<string, IFileFormat> formats;

    public IEnumerable<IFileFormat> Formats => formats.Values;

    public FileGenerator()
    {
        formats = [];
        RegisterFormat(new Original());
    }

    public FileGenerator(IEnumerable<IFileFormat> formats)
        : this()
    {
        if (formats is null == true) throw new ArgumentNullException(nameof(formats));

        foreach (var format in formats)
        {
            if (this.formats.ContainsKey(format.Name) == false)
                this.formats.Add(format.Name, format);
        }
    }

    public FileGenerateResponse Generate(byte[] data, string format)
    {
        if (data is null == true) throw new ArgumentNullException(nameof(data));
        if (formats.ContainsKey(format) == false) throw new NotSupportedException($"This file format is not supported. {format}");

        IFileFormat formatInstance = formats[format];
        FileGenerateResponse newData = formatInstance.Generate(data);

        return newData;
    }

    public IFileGenerator RegisterFormat(IFileFormat format)
    {
        if (formats.ContainsKey(format.Name) == false)
            formats.Add(format.Name, format);
        return this;
    }
}