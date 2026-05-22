// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;
using System.Text;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Observability.Contracts;
using Nalix.Runtime.Pooling;

namespace Backend.Handlers;

[PacketController("ExampleObservabilityAccess")]
public sealed class ObservabilityAccessHandlers
{
    private const int KeyByteLength = 32;

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

        if (!FixedTimeEquals(context.Packet.Key, key))
        {
            return CreateResponse(ProtocolReason.UNAUTHORIZED);
        }

        context.Connection.Level = PermissionLevel.SYSTEM_ADMINISTRATOR;

        return CreateResponse(ProtocolReason.NONE, PermissionLevel.SYSTEM_ADMINISTRATOR);
    }

    private static ValueTask<ObservabilityAccess> CreateResponse(
        ProtocolReason reason,
        PermissionLevel grantedLevel = PermissionLevel.NONE)
    {
        PacketScope<ObservabilityAccess> lease = PacketFactory<ObservabilityAccess>.Acquire();

        try
        {
            ObservabilityAccess response = lease.Value;
            response.Initialize(ObservabilityAccessStage.RESPONSE, reason, grantedLevel);
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

