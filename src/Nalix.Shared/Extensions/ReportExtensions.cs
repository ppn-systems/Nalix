// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides extension methods for saving reports of pool managers.
/// </summary>
public static class ReportExtensions
{
    private static readonly System.String ReportDir = System.IO.Path.Combine(Common.Environment.Directories.LogsDirectory, "reports");

    /// <summary>
    /// Saves the generated report of the manager to a file inside LogsDirectory/reports.
    /// </summary>
    /// <param name="reportable">The reportable manager.</param>
    /// <param name="prefix">Optional filename prefix, e.g. "buffer" or "object".</param>
    /// <returns>The full path of the saved report file.</returns>
    public static System.String SaveReportToFile(this IReportable reportable, System.String prefix = "report")
    {
        System.String report = reportable.GenerateReport();

        _ = System.IO.Directory.CreateDirectory(ReportDir);

        System.String filePath = System.IO.Path.Combine(
            ReportDir, $"{prefix.ToLower()}-report-{System.DateTime.UtcNow:yyyyMMdd-HHmm}.txt");

        System.IO.File.WriteAllText(filePath, report);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{reportable.GetType().Name}] report saved: {filePath}");

        return filePath;
    }
}
