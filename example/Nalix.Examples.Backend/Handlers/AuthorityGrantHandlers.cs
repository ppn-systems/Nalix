// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Examples.Contracts.Packets;

namespace Nalix.Examples.Backend.Handlers;

[PacketController("ExampleAuthorityGrant")]
public sealed class AuthorityGrantHandlers
{
    private const int KeyByteLength = 32;

    [PacketEncryption(true)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketOpcode(AuthorityGrant.OpCodeValue)]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned packet is sent and disposed by the Nalix return handler.")]
    public static ValueTask<AuthorityGrant> HandleAsync(IPacketContext<AuthorityGrant> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AuthorityGrant response = AuthorityGrant.Create();
        AuthorityGrant request = context.Packet;

        if (request.Stage != AuthorityGrantStage.REQUEST || !request.Validate(out _))
        {
            response.Initialize(AuthorityGrantStage.RESPONSE, ProtocolReason.MALFORMED_PACKET);
            return ValueTask.FromResult(response);
        }

        string key = LoadOrCreateSharedKey();
        if (!FixedTimeEquals(request.Key, key))
        {
            response.Initialize(AuthorityGrantStage.RESPONSE, ProtocolReason.UNAUTHORIZED);
            return ValueTask.FromResult(response);
        }

        context.Connection.Level = PermissionLevel.SYSTEM_ADMINISTRATOR;
        response.Initialize(
            AuthorityGrantStage.RESPONSE,
            ProtocolReason.NONE,
            PermissionLevel.SYSTEM_ADMINISTRATOR);

        return ValueTask.FromResult(response);
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
