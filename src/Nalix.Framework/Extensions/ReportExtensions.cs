// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using Nalix.Common.Abstractions;
using Nalix.Common.Environment;

namespace Nalix.Framework.Extensions;

/// <summary>
/// Provides extension methods for saving reports of pool managers.
/// </summary>
public static class ReportExtensions
{
    private static readonly string s_reportDir;

    static ReportExtensions()
    {
        try
        {
            s_reportDir = Path.GetFullPath(Path
                              .Combine(Directories.DataDirectory, "reports"));

            _ = Directory.CreateDirectory(s_reportDir);
        }
        catch
        {
            s_reportDir = Path.Combine(AppContext.BaseDirectory, "reports");
        }
    }

    /// <summary>
    /// Saves the generated report of the manager to a file inside DataDirectory/reports.
    /// </summary>
    /// <param name="this">The reportable manager.</param>
    /// <param name="prefix">Optional filename prefix, e.g. "buffer" or "object".</param>
    /// <returns>The full path of the saved report file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="this"/> is null.</exception>
    /// <remarks>
    /// File-system errors are allowed to propagate to the caller.
    /// </remarks>
    public static string SaveReportToFile(this IReportable @this, string prefix = "null")
    {
        ArgumentNullException.ThrowIfNull(@this, nameof(@this));
        string safePrefix = SANITIZE_FILE_NAME_SEGMENT(prefix?.ToLowerInvariant() ?? "null");
        string report = @this.GenerateReport();
        string filePath = Path.Combine(s_reportDir, $"{safePrefix}-report-{DateTime.UtcNow:yyyyMMdd-HHmm}.txt");

        _ = Directory.CreateDirectory(s_reportDir);
        File.WriteAllText(filePath, report);

        return filePath;
    }

    private static string SANITIZE_FILE_NAME_SEGMENT(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "null";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] result = value.ToCharArray();
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Array.IndexOf(invalidChars, result[i]) >= 0 ? '_' : result[i];
        }

        return new string(result);
    }
}
