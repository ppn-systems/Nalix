// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Environment.IO;
using Nalix.Observability.Contracts;
using Nalix.Runtime.Pooling;

namespace Nalix.Observability.Handlers;

/// <summary>
/// Handles authentication and administrative access requests for observability.
/// </summary>
[PacketController("Nalix.ObservabilityAccess")]
public sealed class ObservabilityAccessHandlers
{
    private static int s_isInitialized;
    private static readonly Lock s_initLock = new();
    private static Bytes32 s_adminKey = Bytes32.Zero;

    /// <summary>
    /// Gets the administrative access key.
    /// </summary>
    public static Bytes32 AdminKey
    {
        get
        {
            if (Volatile.Read(ref s_isInitialized) == 0)
            {
                Initialize();
            }
            return s_adminKey;
        }
    }

    /// <summary>
    /// Initializes the observability access handlers by loading the default administrative key.
    /// </summary>
    public static void Initialize()
    {
        if (Volatile.Read(ref s_isInitialized) != 0)
        {
            return;
        }

        lock (s_initLock)
        {
            if (Volatile.Read(ref s_isInitialized) != 0)
            {
                return;
            }

            LOAD_PRIVATE_KEY(Path.Combine(Directories.ConfigurationDirectory, "observability.private"));
            Volatile.Write(ref s_isInitialized, 1);
        }
    }

    /// <summary>
    /// Sets a custom path for the administrative private key and initializes it.
    /// </summary>
    /// <param name="path">The absolute path to the private key file.</param>
    public static void SetPrivateKeyPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (s_initLock)
        {
            LOAD_PRIVATE_KEY(path);
            Volatile.Write(ref s_isInitialized, 1);
        }
    }

    /// <summary>
    /// Handles an incoming administrative access request and upgrades the connection's permission level if authorized.
    /// </summary>
    /// <param name="context">The packet context containing the request packet and connection state.</param>
    /// <returns>A value task representing the response packet containing the result and permission level.</returns>
    [PacketEncryption(true)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode(ObservabilityAccess.OpCodeValue)]
    public static ValueTask<ObservabilityAccess> HandleAsync(IPacketContext<ObservabilityAccess> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Packet.Stage != ObservabilityAccessStage.REQUEST || !context.Packet.Validate(out _))
        {
            return CREATE_RESPONSE(ProtocolReason.MALFORMED_PACKET);
        }

        if (context.Packet.AccessKey != AdminKey)
        {
            return CREATE_RESPONSE(ProtocolReason.UNAUTHORIZED);
        }

        context.Connection.Level = PermissionLevel.SUPERVISOR;

        return CREATE_RESPONSE(ProtocolReason.NONE, PermissionLevel.SUPERVISOR);
    }

    private static ValueTask<ObservabilityAccess> CREATE_RESPONSE(ProtocolReason reason, PermissionLevel AccessLevel = PermissionLevel.NONE)
    {
        PacketScope<ObservabilityAccess> lease = PacketFactory<ObservabilityAccess>.Acquire();

        try
        {
            ObservabilityAccess response = lease.Value;
            response.Initialize(ObservabilityAccessStage.RESPONSE, reason, AccessLevel);
            return ValueTask.FromResult(response);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design", "CA1031:Do not catch general exception types",
        Justification = "Best-effort file access and fallback generation logic must not throw to ensure system availability.")]
    private static void LOAD_PRIVATE_KEY(string keyPath)
    {
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        }
        catch
        {
            // Ignore directory creation failures
        }

        if (!File.Exists(keyPath))
        {
            byte[] rawBytes = RandomNumberGenerator.GetBytes(Bytes32.Size);
            string hexStr = Convert.ToHexString(rawBytes).ToLowerInvariant();
            try
            {
                File.WriteAllText(keyPath, hexStr + System.Environment.NewLine);
                s_adminKey = new Bytes32(rawBytes);
                return;
            }
            catch
            {
                s_adminKey = new Bytes32(rawBytes);
                return;
            }
        }

        try
        {
            string? hex = null;
            string[] lines = File.ReadAllLines(keyPath);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string trimmed = line.Trim();
                if (trimmed.StartsWith('#'))
                {
                    continue;
                }

                hex = trimmed;
                break;
            }

            Bytes32 parsedKey = Bytes32.Zero;
            bool parseSuccess = false;
            if (!string.IsNullOrWhiteSpace(hex) && hex.Trim().Length == 64)
            {
                try
                {
                    parsedKey = Bytes32.Parse(hex.Trim());
                    parseSuccess = true;
                }
                catch
                {
                    // Ignore parse error and trigger regeneration
                }
            }

            if (!parseSuccess)
            {
                byte[] rawBytes = RandomNumberGenerator.GetBytes(Bytes32.Size);
                string hexStr = Convert.ToHexString(rawBytes).ToLowerInvariant();
                try
                {
                    File.WriteAllText(keyPath, hexStr + System.Environment.NewLine);
                }
                catch
                {
                    // Ignore write failures on recovery path
                }
                s_adminKey = new Bytes32(rawBytes);
                return;
            }

            s_adminKey = parsedKey;
        }
        catch
        {
            s_adminKey = new Bytes32(RandomNumberGenerator.GetBytes(Bytes32.Size));
        }
    }
}
