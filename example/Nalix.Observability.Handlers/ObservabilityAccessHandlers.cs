// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;
using System.Text;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Observability.Contracts;
using Nalix.Runtime.Pooling;

namespace Nalix.Observability.Handlers;

/// <summary>
/// Handles authentication and administrative access requests for observability.
/// </summary>
[PacketController("Nalix.ObservabilityAccess")]
public sealed class ObservabilityAccessHandlers
{
    private const int KeyByteLength = 32;

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
            return CreateResponse(ProtocolReason.MALFORMED_PACKET);
        }

        string key = LoadOrCreateSharedKey();

        if (!FixedTimeEquals(context.Packet.AccessKey, key))
        {
            return CreateResponse(ProtocolReason.UNAUTHORIZED);
        }

        context.Connection.Level = PermissionLevel.SUPERVISOR;

        return CreateResponse(ProtocolReason.NONE, PermissionLevel.SUPERVISOR);
    }

    private static ValueTask<ObservabilityAccess> CreateResponse(ProtocolReason reason, PermissionLevel AccessLevel = PermissionLevel.NONE)
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

    private static string LoadOrCreateSharedKey()
    {
        string path = GetSharedKeyPath();
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (File.Exists(path))
        {
            return File.ReadAllText(path).Trim();
        }

        string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(KeyByteLength)).ToLowerInvariant();
        File.WriteAllText(path, key + System.Environment.NewLine);
        return key;
    }

    private static string GetSharedKeyPath()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "shared");
            if (Directory.Exists(candidate))
            {
                return Path.Combine(candidate, "admin.key");
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "shared", "admin.key");
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left.Trim());
        byte[] rightBytes = Encoding.UTF8.GetBytes(right.Trim());
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}

