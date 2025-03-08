// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Environment;
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
    public static System.String SaveReportToFile(this IReportable @this, System.String prefix = "report")
    {
        System.String report = @this.GenerateReport();
        System.String safePrefix = prefix?.ToLowerInvariant() ?? "report";

        _ = System.IO.Directory.CreateDirectory(ReportDir);

        System.String filePath = System.IO.Path.Combine(ReportDir, $"{safePrefix}-report-{System.DateTime.UtcNow:yyyyMMdd-HHmm}.txt");

        try
        {
            System.IO.File.WriteAllText(filePath, report);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{@this.GetType().Name}] report saved: {filePath}");
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{@this.GetType().Name}] failed to save report to file: {ex}");
        }

        return filePath;
    }
}
