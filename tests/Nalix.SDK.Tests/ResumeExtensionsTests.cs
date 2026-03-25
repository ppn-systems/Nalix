#if DEBUG
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Security.Hashing;
using Nalix.Network.Hosting;
using Nalix.Network.Protocols;
using Nalix.Runtime.Handlers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class ResumeExtensionsTests
{
    private readonly IPacketRegistry _registry;

    public ResumeExtensionsTests()
    {
        _registry = new PacketRegistryFactory().CreateCatalog();
        TestUtils.SetupCertificate();
    }

    [Fact]
    public async Task ResumeSessionAsync_Successful_ReturnsNone()
    {
        int port = TestUtils.GetFreePort();
        Snowflake token = Snowflake.NewId(SnowflakeType.Session);
        byte[] secretBytes = new byte[32];
        secretBytes[0] = 0xAA;
        Bytes32 secret = new(secretBytes);

        // 1. Setup Server with real SessionStore
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        builder.ConfigurePacketRegistry(_registry);
        builder.AddTcp<IntegrationTestProtocol>((ushort)port);
        builder.AddHandler<SessionHandlers>();
        
        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            // 2. Pre-populate SessionStore
            IConnectionHub hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>()!;
            
            // Create a fake connection object to represent the "previous" connection
            SessionSnapshot snapshot = new()
            {
                SessionToken = token.ToUInt56(),
                Secret = secret,
                Algorithm = CipherSuiteType.Chacha20Poly1305,
                ExpiresAtUnixMilliseconds = long.MaxValue
            };
            SessionEntry entry = new(snapshot, UInt56.Zero);

            await hub.SessionStore.StoreAsync(entry);

            // 3. Setup Client
            using TcpSession session = new(new TransportOptions 
            { 
                Secret = secret,
                SessionToken = token,
                EncryptionEnabled = false 
            }, _registry);

            await session.ConnectAsync("127.0.0.1", (ushort)port);

            // 4. Perform Resume
            ProtocolReason result = await session.ResumeSessionAsync();

            // 5. Verify Result
            Assert.Equal(ProtocolReason.NONE, result);
            Assert.NotEqual(token, session.Options.SessionToken); // Token should be rotated
            Assert.True(session.Options.EncryptionEnabled);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    [Fact]
    public async Task ResumeSessionAsync_InvalidProof_ReturnsTokenRevoked()
    {
        int port = TestUtils.GetFreePort();
        Snowflake token = Snowflake.NewId(SnowflakeType.Session);
        
        byte[] serverSecretBytes = new byte[32];
        serverSecretBytes[0] = 0xAA;
        Bytes32 serverSecret = new(serverSecretBytes);

        byte[] clientSecretBytes = new byte[32];
        clientSecretBytes[0] = 0xBB; // Different secret -> invalid proof
        Bytes32 clientSecret = new(clientSecretBytes);

        // 1. Setup Server
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        builder.ConfigurePacketRegistry(_registry);
        builder.AddTcp<IntegrationTestProtocol>((ushort)port);
        builder.AddHandler<SessionHandlers>();
        
        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            IConnectionHub hub = InstanceManager.Instance.GetExistingInstance<IConnectionHub>()!;
            SessionSnapshot snapshot = new()
            {
                SessionToken = token.ToUInt56(),
                Secret = serverSecret,
                ExpiresAtUnixMilliseconds = long.MaxValue
            };
            SessionEntry entry = new(snapshot, UInt56.Zero);
            await hub.SessionStore.StoreAsync(entry);

            // 2. Setup Client with WRONG secret
            using TcpSession session = new(new TransportOptions 
            { 
                Secret = clientSecret,
                SessionToken = token,
                EncryptionEnabled = false 
            }, _registry);

            await session.ConnectAsync("127.0.0.1", (ushort)port);

            // 3. Perform Resume
            ProtocolReason result = await session.ResumeSessionAsync();

            // 4. Verify Result
            // SessionHandlers returns TOKEN_REVOKED if proof is invalid
            Assert.Equal(ProtocolReason.TOKEN_REVOKED, result);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    [Fact]
    public async Task ResumeSessionAsync_ExpiredSession_ReturnsSessionExpired()
    {
        int port = TestUtils.GetFreePort();
        Snowflake token = Snowflake.NewId(SnowflakeType.Session);
        byte[] secretBytes = new byte[32];
        secretBytes[0] = 0xCC;
        Bytes32 secret = new(secretBytes);

        // 1. Setup Server
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        builder.ConfigurePacketRegistry(_registry);
        builder.AddTcp<IntegrationTestProtocol>((ushort)port);
        builder.AddHandler<SessionHandlers>();
        
        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            // Note: SessionHandlers doesn't handle SESSION_EXPIRED directly if token not found, 
            // it's handled by ConsumeAsync returning null.
            // But we don't store it at all to simulate "expired and scavenged" or "never existed".

            // 2. Setup Client
            using TcpSession session = new(new TransportOptions 
            { 
                Address = "127.0.0.1",
                Port = (ushort)port,
                Secret = secret,
                SessionToken = token,
                EncryptionEnabled = false 
            }, _registry);

            Console.WriteLine($"[TEST] Token: {token}");
            Console.WriteLine($"[TEST] Secret Zero: {secret.IsZero}");
            Console.WriteLine($"[TEST] Options Token Empty: {session.Options.SessionToken.IsEmpty}");
            Console.WriteLine($"[TEST] Options Secret Zero: {session.Options.Secret.IsZero}");

            await session.ConnectAsync("127.0.0.1", (ushort)port);

            // 3. Perform Resume
            ProtocolReason result = await session.ResumeSessionAsync();

            // 4. Verify Result
            Assert.Equal(ProtocolReason.SESSION_EXPIRED, result);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }
}
#endif
