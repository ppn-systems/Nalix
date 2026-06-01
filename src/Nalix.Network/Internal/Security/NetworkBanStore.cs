// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.Internal.Security;

/// <summary>
/// Handles highly optimized binary persistence of banned IP addresses.
/// </summary>
internal static class NetworkBanStore
{
    private const byte Version = 1;
    private const int MagicNumber = 0x4E42414E; // 'N' 'B' 'A' 'N'

    /// <summary>
    /// Loads banned IPs from disk, filtering expired entries and applying BanCount decay.
    /// </summary>
    public static List<NetworkBanRecord> Load(string filePath, int maxRecords, TimeSpan decayWindow, long nowTicks, ILogger? logger = null)
    {
        List<NetworkBanRecord> records = new();
        if (!File.Exists(filePath))
        {
            return records;
        }

        int[] backoffDelays = [100, 250, 500, 1000];
        int retryCount = 0;

        while (true)
        {
            try
            {
                using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                using BinaryReader reader = new(fs);

                if (fs.Length < 9)
                {
                    throw new InvalidDataException("File too short for header");
                }

                int magic = reader.ReadInt32();
                if (magic != MagicNumber)
                {
                    throw new InvalidDataException($"Invalid magic number: {magic}");
                }

                byte version = reader.ReadByte();
                if (version != Version)
                {
                    throw new InvalidDataException($"Unsupported version: {version}");
                }

                int recordCount = reader.ReadInt32();
                if (recordCount < 0 || recordCount > maxRecords)
                {
                    throw new InvalidDataException($"Invalid record count: {recordCount}");
                }

                records.Capacity = Math.Min(recordCount, maxRecords);

                for (int i = 0; i < recordCount; i++)
                {
                    byte addrLen = reader.ReadByte();
                    byte[] addressBytes = reader.ReadBytes(addrLen);
                    long bannedUntil = reader.ReadInt64();
                    int banCount = reader.ReadInt32();
                    long lastBanTime = reader.ReadInt64();
                    long lastSeenAt = reader.ReadInt64();

                    if (banCount > 0 && lastBanTime > 0)
                    {
                        long elapsedSinceBan = nowTicks - lastBanTime;
                        if (elapsedSinceBan > decayWindow.Ticks)
                        {
                            int decayAmount = (int)(elapsedSinceBan / decayWindow.Ticks);
                            banCount = Math.Max(0, banCount - decayAmount);
                        }
                    }

                    if (bannedUntil <= nowTicks && banCount <= 0)
                    {
                        continue;
                    }

                    IPAddress ipAddress = new(addressBytes);
                    INetworkEndpoint endpoint = SocketEndpoint.FromIpAddress(ipAddress);

                    records.Add(new NetworkBanRecord(endpoint, bannedUntil, banCount, lastBanTime, lastSeenAt));
                }

                return records;
            }
            catch (IOException ex) when (retryCount < backoffDelays.Length)
            {
                if (logger != null && logger.IsEnabled(LogLevel.Debug))
                {
                    int delay = backoffDelays[retryCount];
                    logger.LogDebug(ex, "[NW.NetworkBanStore] file locked, retrying in {Delay}ms file={FilePath}", delay, filePath);
                }
                System.Threading.Thread.Sleep(backoffDelays[retryCount]);
                retryCount++;
            }
        }
    }

    /// <summary>
    /// Saves banned IPs to disk using an atomic write approach.
    /// </summary>
    public static void Save(string filePath, IEnumerable<NetworkBanRecord> records, int recordCount)
    {
        string tmpPath = filePath + ".tmp";

        try
        {
            List<(NetworkBanRecord Record, byte[] AddrBytes)> validRecords = new(recordCount);
            foreach (NetworkBanRecord record in records)
            {
                if (IPAddress.TryParse(record.Endpoint.Address, out IPAddress? ip))
                {
                    validRecords.Add((record, ip.GetAddressBytes()));
                }
            }

            using (FileStream fs = new(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (BinaryWriter writer = new(fs))
            {
                writer.Write(MagicNumber);
                writer.Write(Version);
                writer.Write(validRecords.Count);

                foreach ((NetworkBanRecord Record, byte[] AddrBytes) in validRecords)
                {
                    writer.Write((byte)AddrBytes.Length);
                    writer.Write(AddrBytes);
                    writer.Write(Record.BannedUntilTicks);
                    writer.Write(Record.BanCount);
                    writer.Write(Record.LastBanTimeTicks);
                    writer.Write(Record.LastSeenAtTicks);
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

internal readonly struct NetworkBanRecord
{
    public readonly int BanCount;
    public readonly long LastSeenAtTicks;
    public readonly long BannedUntilTicks;
    public readonly long LastBanTimeTicks;
    public readonly INetworkEndpoint Endpoint;

    public NetworkBanRecord(INetworkEndpoint endpoint, long bannedUntilTicks, int banCount, long lastBanTimeTicks, long lastSeenAtTicks)
    {
        Endpoint = endpoint;
        BannedUntilTicks = bannedUntilTicks;
        BanCount = banCount;
        LastBanTimeTicks = lastBanTimeTicks;
        LastSeenAtTicks = lastSeenAtTicks;
    }
}
