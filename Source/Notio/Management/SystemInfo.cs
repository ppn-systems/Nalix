using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Notio.Management;

/// <summary>
/// A class containing methods to retrieve system information.
/// </summary>
internal static class SystemInfo
{
    private static readonly Process process = new();

    static SystemInfo()
    {
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            process.StartInfo.FileName = "bash";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            process.StartInfo.FileName = @"C:\Windows\System32\cmd.exe";
        }
    }

    /// <summary>
    /// Parses the provided string for general information.
    /// </summary>
    /// <param name="info">The information string to parse.</param>
    /// <returns>A parsed string or "null" if empty.</returns>
    public static string ParseDefault(this string info)
        => !string.IsNullOrEmpty(info)
        ? info.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[1].Trim() : "null";

    /// <summary>
    /// Parses the provided string for CPU information.
    /// </summary>
    /// <param name="cpu">The CPU information string to parse.</param>
    /// <returns>A string representing the CPU usage percentage.</returns>
    public static string ParseCPU(this string cpu)
    {
        if (string.IsNullOrEmpty(cpu)) return "Error";

        string lastLine = cpu.Split(Environment.NewLine).Last();
        return int.TryParse(lastLine, out int cpuUsage) ? $"{cpuUsage}%" : "0%";
    }

    /// <summary>
    /// Parses the provided string for memory information.
    /// </summary>
    /// <param name="memory">The memory information string to parse.</param>
    /// <returns>A string representing used memory and percentage of usage.</returns>
    public static string ParseMemory(this string memory)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Unsupported OS";

        var memoryInfo = memory.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                               .Select(line => line.Split(':'))
                               .Where(parts => parts.Length == 2 &&
                                              (parts[0].Trim().Equals("MemFree", StringComparison.OrdinalIgnoreCase) ||
                                               parts[0].Trim().Equals("MemTotal", StringComparison.OrdinalIgnoreCase)))
                               .ToDictionary(parts => parts[0].Trim(), parts => long.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));

        double freeMemoryMB = memoryInfo.GetValueOrDefault("MemFree", 0) / 1024.0;
        double totalMemoryMB = memoryInfo.GetValueOrDefault("MemTotal", 0) / 1024.0;
        double usedMemoryPercentage = (totalMemoryMB - freeMemoryMB) / totalMemoryMB * 100;

        return $"{totalMemoryMB - freeMemoryMB:F2} MB ({usedMemoryPercentage:F2}%) / {totalMemoryMB:F2} MB";
    }

    /// <summary>
    /// Executes a system command and returns the result.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>The output of the executed command or an error message.</returns>
    public static string RunCommand(string command)
    {
        try
        {
            process.StartInfo.Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/C {command}" : $"-c \"{command}\"";
            StringBuilder output = new();

            process.Start();

            while (!process.StandardOutput.EndOfStream)
            {
                output.AppendLine(process.StandardOutput.ReadLine());
            }

            process.WaitForExit();
            return output.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message} (Command: {command})";
        }
    }

    /// <summary>
    /// Terminates the running process if not already exited.
    /// </summary>
    public static void StopProcess()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping process: {ex.Message}");
        }
    }
}