// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Environment;
using Nalix.Framework.Injection;

namespace Nalix.Framework.Extensions;

/// <summary>
/// Provides extension methods for saving reports of pool managers.
/// </summary>
public static class ReportExtensions
{
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly string s_reportDir;

    static ReportExtensions()
    {
        s_reportDir = Path.GetFullPath(Path
                          .Combine(Directories.DataDirectory, "reports"));

        _ = Directory.CreateDirectory(s_reportDir);
    }

    /// <summary>
    /// Saves the generated report of the manager to a file inside DataDirectory/reports.
    /// </summary>
    /// <param name="this">The reportable manager.</param>
    /// <param name="prefix">Optional filename prefix, e.g. "buffer" or "object".</param>
    /// <returns>The full path of the saved report file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="this"/> is null.</exception>
    /// <remarks>
    /// File-system errors are logged internally; the method still returns the intended output path.
    /// </remarks>
    public static string SaveReportToFile(this IReportable @this, string prefix = "null")
    {
        ArgumentNullException.ThrowIfNull(@this, nameof(@this));

        string report = @this.GenerateReport();
        string safePrefix = prefix?.ToLowerInvariant() ?? "null";

        _ = Directory.CreateDirectory(s_reportDir);

        string filePath = Path.Combine(s_reportDir, $"{safePrefix}-report-{DateTime.UtcNow:yyyyMMdd-HHmm}.txt");

        try
        {
            File.WriteAllText(filePath, report);

            // Select last 3 segments, or fewer if there are less than 3
            string[] segments = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int segmentCount = segments.Length;
            string lastSegments = string.Join(Path.DirectorySeparatorChar
                                                      .ToString(), Enumerable
                                                      .Skip(segments, Math
                                                      .Max(0, segmentCount - 3)));

            if (s_logger?.IsEnabled(LogLevel.Information) == true)
            {
                s_logger.LogInformation("[RP.{Type}] report-saved path={Path}", @this.GetType().Name, lastSegments);
            }
        }
        catch (IOException ex)
        {
            if (s_logger?.IsEnabled(LogLevel.Error) == true)
            {
                s_logger.LogError(ex, "[RP.{Type}] failed-save-report", @this.GetType().Name);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            if (s_logger?.IsEnabled(LogLevel.Error) == true)
            {
                s_logger.LogError(ex, "[RP.{Type}] failed-save-report", @this.GetType().Name);
            }
        }
        catch (NotSupportedException ex)
        {
            if (s_logger?.IsEnabled(LogLevel.Error) == true)
            {
                s_logger.LogError(ex, "[RP.{Type}] failed-save-report", @this.GetType().Name);
            }
        }

        return filePath;
    }
}
