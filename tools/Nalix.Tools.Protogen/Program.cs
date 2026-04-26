using System;
using System.Collections.Generic;
using Nalix.Tools.Protogen.Application;
using Nalix.Tools.Protogen.Generators.TypeScript;
using Nalix.Tools.Protogen.Infrastructure.IO;
using Nalix.Tools.Protogen.Infrastructure.Reflection;

namespace Nalix.Tools.Protogen;

internal class Program
{
    private static void Main(string[] args)
    {
        CommandLineArgs options = CommandLineArgs.Parse(args);

        if (options.ShowHelp || options.InputPaths.Count == 0 || string.IsNullOrEmpty(options.OutputDirectory))
        {
            options.PrintUsage();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=====================================================");
        Console.WriteLine("         NALIX PROTOGEN - TS CLIENT GENERATOR       ");
        Console.WriteLine("=====================================================");
        Console.ResetColor();

        Console.WriteLine($"Input  : {string.Join(", ", options.InputPaths)}");
        Console.WriteLine($"Output : {options.OutputDirectory}");
        Console.WriteLine($"Lang   : {options.Language}");
        Console.WriteLine("-----------------------------------------------------");

        try
        {
            // DI Setup
            var scanner = new ReflectionPacketScanner();
            var fileWriter = new LocalFileWriter();
            var generator = options.Language.ToLowerInvariant() switch
            {
                "ts" or "typescript" => new TypeScriptGenerator(),
                _ => throw new NotSupportedException($"Language '{options.Language}' is not supported yet.")
            };

            var engine = new ProtogenEngine(scanner, generator, fileWriter);
            engine.Run(options.InputPaths, options.OutputDirectory);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FATAL] Generation failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
        }
    }
}

internal class CommandLineArgs
{
    public List<string> InputPaths { get; } = new();
    public string OutputDirectory { get; set; } = string.Empty;
    public string Language { get; set; } = "ts";
    public bool ShowHelp { get; set; }

    public static CommandLineArgs Parse(string[] args)
    {
        CommandLineArgs options = new();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h": options.ShowHelp = true; break;
                case "--input":
                case "-i": options.InputPaths.Add(args[++i]); break;
                case "--output":
                case "-o": options.OutputDirectory = args[++i]; break;
                case "--lang":
                case "-l": options.Language = args[++i]; break;
                case "--": break;
            }
        }
        return options;
    }

    public void PrintUsage()
    {
        Console.WriteLine("Usage: Nalix.Tools.Protogen [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  -i, --input <path>      Input .dll file or directory (can be used multiple times) (required)");
        Console.WriteLine("  -o, --output <path>     Output directory for generated client code (required)");
        Console.WriteLine("  -l, --lang <lang>       Target language: ts (default: ts)");
        Console.WriteLine("  -h, --help              Show this help message");
    }
}
