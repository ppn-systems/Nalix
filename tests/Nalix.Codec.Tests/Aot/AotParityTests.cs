// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Nalix.Codec.Tests.Aot;

public sealed class AotParityTests
{
    private static readonly String RepositoryRoot = FindRepositoryRoot();
    private static readonly String HarnessProject = Path.Combine(RepositoryRoot, "tests", "Nalix.Codec.AotCompare", "Nalix.Codec.AotCompare.csproj");
    private static readonly String ResultsDir = Path.Combine(RepositoryRoot, "tests", "Nalix.Codec.AotCompare", "artifacts");

    [Fact]
    public async Task NativeAot_OutputMatchesJit_ForCodecScenarios()
    {
        Directory.CreateDirectory(ResultsDir);

        String jitJson = await RunDotnetAsync("run", $"--project \"{HarnessProject}\" -c Release --no-restore");
        String publishOutput = await RunDotnetAsync(
            "publish",
            $"\"{HarnessProject}\" -c Release -r win-x64 -p:PublishAot=true -p:DefineConstants=NALIX_AOT --self-contained true --no-restore");

        String nativeExe = FindNativeExecutable(publishOutput);
        String aotJson = await RunProcessAsync(nativeExe, String.Empty, Path.GetDirectoryName(nativeExe)!);

        await File.WriteAllTextAsync(Path.Combine(ResultsDir, "jit.json"), jitJson);
        await File.WriteAllTextAsync(Path.Combine(ResultsDir, "aot.json"), aotJson);

        ScenarioResult[] jit = Parse(jitJson);
        ScenarioResult[] aot = Parse(aotJson);

        Assert.NotEmpty(jit);
        Assert.Equal(jit.Length, aot.Length);
        Assert.All(jit, static scenario => Assert.True(scenario.Passed, scenario.Error));
        Assert.All(aot, static scenario => Assert.True(scenario.Passed, scenario.Error));

        Dictionary<String, ScenarioResult> aotByName = aot.ToDictionary(static x => x.Name);
        foreach (ScenarioResult expected in jit)
        {
            ScenarioResult actual = aotByName[expected.Name];
            Assert.Equal(expected.Length, actual.Length);
            Assert.Equal(expected.Sha256, actual.Sha256);
            Assert.Equal(expected.Details, actual.Details);
        }
    }

    private static ScenarioResult[] Parse(String output)
    {
        Int32 start = output.IndexOf('[', StringComparison.Ordinal);
        Int32 end = output.LastIndexOf(']');
        Assert.True(start >= 0 && end > start, output);
        return JsonSerializer.Deserialize<ScenarioResult[]>(output[start..(end + 1)], new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static String FindNativeExecutable(String publishOutput)
    {
        String marker = "Nalix.Codec.AotCompare -> ";
        String? publishDir = publishOutput
            .Split([System.Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(line => line.Contains(marker, StringComparison.Ordinal))
            .Select(line => line[(line.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..].Trim())
            .LastOrDefault();

        if (publishDir is null || !Directory.Exists(publishDir))
        {
            publishDir = Path.Combine(RepositoryRoot, "tests", "Nalix.Codec.AotCompare", "bin", "Release", "net10.0", "win-x64", "publish");
        }

        String exe = Path.Combine(publishDir, "Nalix.Codec.AotCompare.exe");
        Assert.True(File.Exists(exe), $"Native executable not found: {exe}\nPublish output:\n{publishOutput}");
        return exe;
    }

    private static async Task<String> RunDotnetAsync(String verb, String args)
        => await RunProcessAsync("dotnet", $"{verb} {args}", RepositoryRoot);

    private static async Task<String> RunProcessAsync(String fileName, String arguments, String workingDirectory)
    {
        ProcessStartInfo startInfo = new(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using Process process = Process.Start(startInfo)!;
        String stdout = await process.StandardOutput.ReadToEndAsync();
        String stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"Command failed ({process.ExitCode}): {fileName} {arguments}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        return stdout;
    }

    private static String FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !IsRepositoryRoot(current.FullName))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
        }

        return current.FullName;
    }

    private static Boolean IsRepositoryRoot(String path)
        => File.Exists(Path.Combine(path, "README.md"))
        && Directory.Exists(Path.Combine(path, "src"))
        && Directory.Exists(Path.Combine(path, "tests"));

    private sealed record ScenarioResult(String Name, String Category, Boolean Passed, Int32? Length, String? Sha256, String? Details, String? Error);
}
