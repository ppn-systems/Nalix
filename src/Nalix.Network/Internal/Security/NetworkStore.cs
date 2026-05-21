// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Nalix.Abstractions.Exceptions;

namespace Nalix.Network.Internal.Security;

/// <summary>
/// Handles highly optimized persistence of IP networks and addresses in plain-text format.
/// </summary>
internal static class NetworkStore
{
    /// <summary>
    /// Loads IP networks from a line-separated plain-text file.
    /// </summary>
    public static List<IPNetwork> Load(string filePath, int maxRecords)
    {
        List<IPNetwork> records = new();
        if (!File.Exists(filePath))
        {
            return records;
        }

        try
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            using StreamReader reader = new(fs);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (records.Count >= maxRecords)
                {
                    break;
                }

                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                {
                    continue;
                }

                if (IPNetwork.TryParse(line, out IPNetwork network))
                {
                    records.Add(network);
                }
                else if (IPAddress.TryParse(line, out IPAddress? ip))
                {
                    records.Add(new IPNetwork(ip, ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            records.Clear();
        }

        return records;
    }

    /// <summary>
    /// Saves IP networks to disk using an atomic write approach.
    /// </summary>
    public static void Save(string filePath, IEnumerable<IPNetwork> records, string listDescription)
    {
        string tmpPath = filePath + ".tmp";

        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            using (FileStream fs = new(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (StreamWriter writer = new(fs))
            {
                writer.WriteLine($"# Nalix Connection Guard - {listDescription}");
                writer.WriteLine($"# Generated at {DateTime.UtcNow:u}");
                writer.WriteLine("# Each non-empty line must be a valid IP address or CIDR range (e.g. 192.168.1.0/24).");
                writer.WriteLine();

                foreach (IPNetwork network in records)
                {
                    writer.WriteLine(network.ToString());
                }

                fs.Flush(true); // Ensure physical write to disk
            }

            File.Move(tmpPath, filePath, overwrite: true);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // Do not crash server on save error
        }
        finally
        {
            if (File.Exists(tmpPath))
            {
                try
                {
                    File.Delete(tmpPath);
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }
    }
}
