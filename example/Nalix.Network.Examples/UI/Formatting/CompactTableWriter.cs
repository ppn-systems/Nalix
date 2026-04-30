// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text;

namespace Nalix.Network.Examples.UI.Formatting;

/// <summary>
/// Writes compact, lib-style tables for known report data list keys.
/// Each table groups multiple columns into compact display (e.g. STORAGE (A/M)).
/// </summary>
internal static class CompactTableWriter
{
    /// <summary>
    /// Dispatches to a known compact table format based on the key name.
    /// Falls back to a 4-column generic layout for unknown keys.
    /// </summary>
    public static void Write(StringBuilder sb, string key, IList<Dictionary<string, object>> list)
    {
        if (key is "Pools" && list[0].ContainsKey("Gets"))
            WriteObjectPoolTable(sb, list);
        else if (key is "Pools" && list[0].ContainsKey("BufferSize"))
            WriteBufferPoolTable(sb, list);
        else if (key is "Recurring")
            WriteRecurringTable(sb, list);
        else if (key is "TopRecurringByFailures")
            WriteTopRecurringTable(sb, list);
        else if (key is "TopRunningWorkers")
            WriteWorkersTable(sb, list);
        else if (key is "SampleConnections")
            WriteConnectionsTable(sb, list);
        else if (key is "Instances")
            WriteInstancesTable(sb, list);
        else if (key is "UnhealthyPools")
            WriteUnhealthyTable(sb, list);
        else
            WriteFallbackTable(sb, list);
    }

    // ── ObjectPool ───────────────────────────────────────────────────────────

    private static void WriteObjectPoolTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("TYPE                         | STORAGE (A/M)     | TRAFFIC (G/R)     | HIT%   | STATUS");
        sb.AppendLine("-----------------------------+-------------------+-------------------+--------+-------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string type    = Truncate(Str(d, "Type"), 28).PadRight(28);
            string storage = $"{Compact(Long(d, "Available"))} / {Compact(Long(d, "MaxCapacity"))}";
            string traffic = $"{Compact(Long(d, "Gets"))} / {Compact(Long(d, "Hits"))}";
            double hit     = Dbl(d, "HitRate");
            string st      = Str(d, "Status");
            sb.Append(CultureInfo.InvariantCulture, $"{type} | {storage,-17} | {traffic,-17} | {hit,5:F1}% | {st}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------------------+-------------------+-------------------+--------+-------");
    }

    // ── BufferPool ───────────────────────────────────────────────────────────

    private static void WriteBufferPoolTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("SIZE     | CAPACITY (F/T/I)         | OPS (H/E/S)         | USAGE % | MISS %");
        sb.AppendLine("---------+--------------------------+---------------------+---------+-------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            long size = Long(d, "BufferSize");
            string cap = $"{Compact(Long(d, "Free"))} / {Compact(Long(d, "Total"))} / {Compact(Long(d, "Initial"))}";
            string ops = $"{Compact(Long(d, "Hits"))} / {Compact(Long(d, "Expands"))} / {Compact(Long(d, "Shrinks"))}";
            double usage = Dbl(d, "UsageRatio") * 100.0;
            double miss  = Dbl(d, "MissRate") * 100.0;
            sb.Append(CultureInfo.InvariantCulture, $"{size,8} | {cap,-24} | {ops,-19} | {usage,6:F2}% | {miss:F2}%");
            sb.AppendLine();
        }
        sb.AppendLine("---------+--------------------------+---------------------+---------+-------");
    }

    // ── Recurring ────────────────────────────────────────────────────────────

    private static void WriteRecurringTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("NAME                         | RUNS (T/F)    | RUN | SCHEDULE (L/N)          | TAG");
        sb.AppendLine("-----------------------------+---------------+-----+-------------------------+----------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string nm    = Truncate(Str(d, "Name"), 28).PadRight(28);
            string rf    = $"{Compact(Long(d, "TotalRuns"))} / {Long(d, "ConsecutiveFailures")}";
            bool running = Bool(d, "IsRunning");
            string sched = $"{FmtTime(d, "LastRunUtc")} / {FmtTime(d, "NextRunUtc")}";
            string tag   = Str(d, "Tag");
            sb.Append(CultureInfo.InvariantCulture, $"{nm} | {rf,-13} | {(running ? "yes" : " no"),3} | {sched,-23} | {tag}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------------------+---------------+-----+-------------------------+----------");
    }

    // ── TopRecurringByFailures ───────────────────────────────────────────────

    private static void WriteTopRecurringTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("NAME                         | FAILURES | LAST RUN | TAG");
        sb.AppendLine("-----------------------------+----------+----------+----------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string nm   = Truncate(Str(d, "Name"), 28).PadRight(28);
            long fails  = Long(d, "ConsecutiveFailures");
            string last = FmtTime(d, "LastRunUtc");
            string tag  = Str(d, "Tag");
            sb.Append(CultureInfo.InvariantCulture, $"{nm} | {fails,8} | {last} | {tag}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------------------+----------+----------+----------");
    }

    // ── Workers ──────────────────────────────────────────────────────────────

    private static void WriteWorkersTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("ID               | NAME                         | GROUP                        | PROGRESS");
        sb.AppendLine("-----------------+------------------------------+------------------------------+---------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string id   = Truncate(Str(d, "Id"), 16).PadRight(16);
            string name = Truncate(Str(d, "Name"), 28).PadRight(28);
            string grp  = Truncate(Str(d, "Group"), 28).PadRight(28);
            long prog   = Long(d, "Progress");
            sb.Append(CultureInfo.InvariantCulture, $"{id} | {name} | {grp} | {Compact(prog),8}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------+------------------------------+------------------------------+---------");
    }

    // ── Connections ──────────────────────────────────────────────────────────

    private static void WriteConnectionsTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("ID               | USERNAME         | LEVEL      | ALGORITHM  | UPTIME");
        sb.AppendLine("-----------------+------------------+------------+------------+-------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string id   = Truncate(Str(d, "ID"), 16).PadRight(16);
            string user = Truncate(Str(d, "Username"), 16).PadRight(16);
            string lvl  = Truncate(Str(d, "Level"), 10).PadRight(10);
            string algo = Truncate(Str(d, "Algorithm"), 10).PadRight(10);
            long up     = Long(d, "UpTime");
            sb.Append(CultureInfo.InvariantCulture, $"{id} | {user} | {lvl} | {algo} | {up}s");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------+------------------+------------+------------+-------");
    }

    // ── Instances ────────────────────────────────────────────────────────────

    private static void WriteInstancesTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("TYPE                                          | DISPOSABLE | SOURCE");
        sb.AppendLine("----------------------------------------------+------------+--------------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string type = Truncate(Str(d, "Type"), 45).PadRight(45);
            bool disp   = Bool(d, "IsDisposable");
            string src  = Str(d, "Source");
            sb.Append(CultureInfo.InvariantCulture, $"{type} | {(disp ? "Yes" : "No "),10} | {src}");
            sb.AppendLine();
        }
        sb.AppendLine("----------------------------------------------+------------+--------------");
    }

    // ── Unhealthy ────────────────────────────────────────────────────────────

    private static void WriteUnhealthyTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        sb.AppendLine("TYPE                         | FAILURES | LAST ACCESS      | OUTSTANDING");
        sb.AppendLine("-----------------------------+----------+------------------+------------");
        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var d = list[r];
            string type = Truncate(Str(d, "Type"), 28).PadRight(28);
            long fails  = Long(d, "ConsecutiveFailures");
            string last = FmtTime(d, "LastAccessUtc");
            long out_   = Long(d, "Outstanding");
            sb.Append(CultureInfo.InvariantCulture, $"{type} | {fails,8} | {last,16} | {out_,10}");
            sb.AppendLine();
        }
        sb.AppendLine("-----------------------------+----------+------------------+------------");
    }

    // ── Fallback ─────────────────────────────────────────────────────────────

    private static void WriteFallbackTable(StringBuilder sb, IList<Dictionary<string, object>> list)
    {
        var first = list[0];
        string[] cols = new string[Math.Min(first.Count, 4)];
        int ci = 0;
        foreach (string k in first.Keys) { cols[ci++] = k; if (ci >= cols.Length) break; }

        for (int c = 0; c < cols.Length; c++)
        {
            AppendPadded(sb, cols[c], 18);
            if (c < cols.Length - 1) sb.Append(" | ");
        }
        sb.AppendLine();
        for (int c = 0; c < cols.Length; c++)
        {
            sb.Append('-', 18);
            if (c < cols.Length - 1) sb.Append("-+-");
        }
        sb.AppendLine();

        int limit = Math.Min(list.Count, 50);
        for (int r = 0; r < limit; r++)
        {
            var row = list[r];
            for (int c = 0; c < cols.Length; c++)
            {
                string val = row.TryGetValue(cols[c], out object? v) ? ReportDataFormatter.FormatScalar(v) : "-";
                AppendPadded(sb, val, 18);
                if (c < cols.Length - 1) sb.Append(" | ");
            }
            sb.AppendLine();
        }
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    private static string Str(Dictionary<string, object> d, string k)
        => d.TryGetValue(k, out object? v) ? (v?.ToString() ?? "-") : "-";

    private static long Long(Dictionary<string, object> d, string k)
        => d.TryGetValue(k, out object? v) ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : 0;

    private static double Dbl(Dictionary<string, object> d, string k)
        => d.TryGetValue(k, out object? v) ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0.0;

    private static bool Bool(Dictionary<string, object> d, string k)
        => d.TryGetValue(k, out object? v) && v is bool b && b;

    private static string FmtTime(Dictionary<string, object> d, string k)
    {
        if (!d.TryGetValue(k, out object? v)) return "--:--:--";
        return v switch
        {
            DateTimeOffset dto => dto == DateTimeOffset.MinValue ? "--:--:--" : dto.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            DateTime dt        => dt == DateTime.MinValue ? "--:--:--" : dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            _                  => "--:--:--"
        };
    }

    private static string Compact(long value)
    {
        if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
        if (value < 1_000_000) return $"{value / 1000.0:F1}k";
        return $"{value / 1_000_000.0:F1}M";
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : $"{s.AsSpan(0, max - 2)}..";

    private static void AppendPadded(StringBuilder sb, string val, int width)
    {
        if (val.Length > width)
        {
            sb.Append(val.AsSpan(0, width - 2));
            sb.Append("..");
        }
        else
        {
            sb.Append(val);
            for (int i = val.Length; i < width; i++) sb.Append(' ');
        }
    }
}
