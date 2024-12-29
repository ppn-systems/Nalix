using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Notio.Infrastructure.Management;

/// <summary>
/// Lớp chứa các phương thức để lấy thông tin hệ thống.
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
    /// Phân tích chuỗi thông tin mặc định.
    /// </summary>
    /// <param name="info">Thông tin cần phân tích.</param>
    /// <returns>Chuỗi đã phân tích hoặc "null" nếu không có dữ liệu.</returns>
    public static string ParseDefault(this string info)
        => !string.IsNullOrEmpty(info)
        ? info.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[1].Trim() : "null";

    /// <summary>
    /// Phân tích chuỗi thông tin CPU.
    /// </summary>
    /// <param name="cpu">Chuỗi thông tin CPU cần phân tích.</param>
    /// <returns>Chuỗi phần trăm tải CPU hoặc thông báo lỗi.</returns>
    public static string ParseCPU(this string cpu)
    {
        if (string.IsNullOrEmpty(cpu)) return "Error";

        string lastLine = cpu.Split(Environment.NewLine).Last();
        return int.TryParse(lastLine, out int cpuUsage) ? $"{cpuUsage}%" : "0%";
    }

    /// <summary>
    /// Phân tích chuỗi thông tin bộ nhớ.
    /// </summary>
    /// <param name="memory">Chuỗi thông tin bộ nhớ cần phân tích.</param>
    /// <returns>Chuỗi mô tả trạng thái bộ nhớ hoặc thông báo lỗi.</returns>
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
    /// Chạy lệnh hệ thống và trả về kết quả.
    /// </summary>
    /// <param name="command">Lệnh cần chạy.</param>
    /// <returns>Kết quả đầu ra của lệnh.</returns>
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
    /// Dừng quá trình.
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