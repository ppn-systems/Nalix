// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Nalix.Abstractions.Networking;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.RateLimiting;

/// <summary>
/// Handles highly optimized binary persistence of banned IP addresses.
/// </summary>
internal static class BannedIpStore
{
    private const int MagicNumber = 0x4E42414E; // 'N' 'B' 'A' 'N'
    private const byte Version = 1;

    /// <summary>
    /// Loads banned IPs from disk, filtering expired entries and applying BanCount decay.
    /// </summary>
    public static List<BannedIpRecord> Load(string filePath, int maxRecords, TimeSpan decayWindow, long nowTicks)
    {
        List<BannedIpRecord> records = new();
        if (!File.Exists(filePath))
        {
            return records;
        }

        try
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            using BinaryReader reader = new(fs);

            if (fs.Length < 9)
            {
                return records; // Too short for header
            }

            int magic = reader.ReadInt32();
            if (magic != MagicNumber)
            {
                return records; // Invalid magic
            }

            byte version = reader.ReadByte();
            if (version != Version)
            {
                return records; // Unsupported version
            }

            int recordCount = reader.ReadInt32();
            if (recordCount < 0 || recordCount > maxRecords)
            {
                // Reject entire file if record count is out of bounds (corrupted)
                return records;
            }

            // Pre-allocate capacity
            records.Capacity = Math.Min(recordCount, maxRecords);

            for (int i = 0; i < recordCount; i++)
            {
                byte addrLen = reader.ReadByte();
                byte[] addressBytes = reader.ReadBytes(addrLen);
                long bannedUntil = reader.ReadInt64();
                int banCount = reader.ReadInt32();
                long lastBanTime = reader.ReadInt64();
                long lastSeenAt = reader.ReadInt64();

                // Calculate decay
                if (banCount > 0 && lastBanTime > 0)
                {
                    long elapsedSinceBan = nowTicks - lastBanTime;
                    if (elapsedSinceBan > decayWindow.Ticks)
                    {
                        // Decay the ban count based on how many windows have passed
                        int decayAmount = (int)(elapsedSinceBan / decayWindow.Ticks);
                        banCount = Math.Max(0, banCount - decayAmount);
                    }
                }

                // If fully expired and ban count decayed to 0, we can skip loading it
                if (bannedUntil <= nowTicks && banCount <= 0)
                {
                    continue;
                }

                IPAddress ipAddress = new(addressBytes);
                INetworkEndpoint endpoint = SocketEndpoint.FromIpAddress(ipAddress);

                records.Add(new BannedIpRecord(endpoint, bannedUntil, banCount, lastBanTime, lastSeenAt));
            }
        }
        catch (Exception)
        {
            // Corrupted file or IO error, reject all to start fresh
            records.Clear();
        }

        return records;
    }

    /// <summary>
    /// Saves banned IPs to disk using an atomic write approach.
    /// </summary>
    public static void Save(string filePath, IEnumerable<BannedIpRecord> records, int recordCount)
    {
        string tmpPath = filePath + ".tmp";

        try
        {
            using (FileStream fs = new(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (BinaryWriter writer = new(fs))
            {
                writer.Write(MagicNumber);
                writer.Write(Version);
                writer.Write(recordCount);

                foreach (BannedIpRecord record in records)
                {
                    if (!IPAddress.TryParse(record.Endpoint.Address, out IPAddress? ip))
                    {
                        continue;
                    }

                    byte[] addrBytes = ip.GetAddressBytes();
                    writer.Write((byte)addrBytes.Length);
                    writer.Write(addrBytes);
                    writer.Write(record.BannedUntilTicks);
                    writer.Write(record.BanCount);
                    writer.Write(record.LastBanTimeTicks);
                    writer.Write(record.LastSeenAtTicks);
                }

                fs.Flush(true); // Ensure physical write to disk
            }

            File.Move(tmpPath, filePath, overwrite: true);
        }
        catch (Exception)
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
                catch
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }
    }
}

internal readonly struct BannedIpRecord
{
    public readonly INetworkEndpoint Endpoint;
    public readonly long BannedUntilTicks;
    public readonly int BanCount;
    public readonly long LastBanTimeTicks;
    public readonly long LastSeenAtTicks;

    public BannedIpRecord(INetworkEndpoint endpoint, long bannedUntilTicks, int banCount, long lastBanTimeTicks, long lastSeenAtTicks)
    {
        Endpoint = endpoint;
        BannedUntilTicks = bannedUntilTicks;
        BanCount = banCount;
        LastBanTimeTicks = lastBanTimeTicks;
        LastSeenAtTicks = lastSeenAtTicks;
    }
}
