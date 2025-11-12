// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using Nalix.Common.Diagnostics;
using Nalix.Common.Environment;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;

namespace Nalix.Framework.Extensions;

/// <summary>
/// Provides extension methods for saving reports of pool managers.
/// </summary>
public static class ReportExtensions
{
    private static readonly string ReportDir;

    static ReportExtensions()
    {
        ReportDir = Path.GetFullPath(Path
                                  .Combine(Directories.DataDirectory, "reports"));

        _ = Directory.CreateDirectory(ReportDir);
    }

    /// <summary>
    /// Saves the generated report of the manager to a file inside DataDirectory/reports.
    /// </summary>
    /// <param name="this">The reportable manager.</param>
    /// <param name="prefix">Optional filename prefix, e.g. "buffer" or "object".</param>
    /// <returns>The full path of the saved report file.</returns>
    public static string SaveReportToFile(this IReportable @this, string prefix = "null")
    {
        string report = @this.GenerateReport();
        string safePrefix = prefix?.ToLowerInvariant() ?? "null";

        _ = Directory.CreateDirectory(ReportDir);

        string filePath = Path.Combine(ReportDir, $"{safePrefix}-report-{DateTime.UtcNow:yyyyMMdd-HHmm}.txt");

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

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[RP.{@this.GetType().Name}] report-saved path={lastSegments}");
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[RP.{@this.GetType().Name}] failed-save-report", ex);
        }

        return filePath;
    }
}
