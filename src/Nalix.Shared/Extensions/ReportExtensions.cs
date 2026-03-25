// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Environment;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides extension methods for saving reports of pool managers.
/// </summary>
public static class ReportExtensions
{
    private static readonly System.String ReportDir;

    static ReportExtensions()
    {
        ReportDir = System.IO.Path.GetFullPath(System.IO.Path
                                  .Combine(Directories.DataDirectory, "reports"));

        _ = System.IO.Directory.CreateDirectory(ReportDir);
    }

    /// <summary>
    /// Saves the generated report of the manager to a file inside DataDirectory/reports.
    /// </summary>
    /// <param name="this">The reportable manager.</param>
    /// <param name="prefix">Optional filename prefix, e.g. "buffer" or "object".</param>
    /// <returns>The full path of the saved report file.</returns>
    public static System.String SaveReportToFile(this IReportable @this, System.String prefix = "null")
    {
        System.String report = @this.GenerateReport();
        System.String safePrefix = prefix?.ToLowerInvariant() ?? "null";

        _ = System.IO.Directory.CreateDirectory(ReportDir);

        System.String filePath = System.IO.Path.Combine(ReportDir, $"{safePrefix}-report-{System.DateTime.UtcNow:yyyyMMdd-HHmm}.txt");

        try
        {
            System.IO.File.WriteAllText(filePath, report);

            // Select last 3 segments, or fewer if there are less than 3
            System.String[] segments = filePath.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            System.Int32 segmentCount = segments.Length;
            System.String lastSegments = System.String.Join(System.IO.Path.DirectorySeparatorChar
                                                      .ToString(), System.Linq.Enumerable
                                                      .Skip(segments, System.Math
                                                      .Max(0, segmentCount - 3)));

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[RP.{@this.GetType().Name}] report-saved path={lastSegments}");
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[RP.{@this.GetType().Name}] failed-save-report", ex);
        }

        return filePath;
    }
}
