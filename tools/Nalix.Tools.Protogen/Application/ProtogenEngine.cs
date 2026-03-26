using System;
using System.Collections.Generic;
using Nalix.Tools.Protogen.Domain.Interfaces;

namespace Nalix.Tools.Protogen.Application;

public class ProtogenEngine
{
    private readonly IPacketScanner _scanner;
    private readonly ICodeGenerator _generator;
    private readonly IFileWriter _fileWriter;

    public ProtogenEngine(IPacketScanner scanner, ICodeGenerator generator, IFileWriter fileWriter)
    {
        _scanner = scanner;
        _generator = generator;
        _fileWriter = fileWriter;
    }

    public void Run(IEnumerable<string> inputPaths, string outputDirectory)
    {
        Console.WriteLine("[1/3] Loading Assemblies and Analyzing...");
        var packets = _scanner.Scan(inputPaths);
        Console.WriteLine($"      Found {packets.Count} packets.");

        Console.WriteLine($"[2/3] Generating {_generator.LanguageName} Client...");
        var generatedFiles = _generator.Generate(packets);

        Console.WriteLine("[3/3] Saving Files...");
        _fileWriter.CreateDirectory(outputDirectory);
        foreach (var file in generatedFiles)
        {
            string filePath = System.IO.Path.Combine(outputDirectory, file.Key);
            _fileWriter.WriteAllText(filePath, file.Value);
            Console.WriteLine($"      -> {file.Key}");
        }

        Console.WriteLine("[3/3] Done!");
    }
}
