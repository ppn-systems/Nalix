using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.DataFrames;
using Nalix.Framework.Injection;
using Nalix.Hosting;
using Nalix.Runtime.Handlers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class HandshakeIntegrationTests : IDisposable
{
    private readonly string _certPath;
    private readonly Bytes32 _serverPublicKey;

    public HandshakeIntegrationTests()
    {
        if (!PacketRegistry.IsBuilt)
            PacketRegistry.Build();
        // Setup Server Identity
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        _certPath = null!;
        while (current != null)
        {
            string candidate = Path.Combine(current, "shared", "certificate.private");
            if (File.Exists(candidate))
            {
                _certPath = candidate;
                break;
            }
            current = Path.GetDirectoryName(current);
        }

        if (_certPath == null)
        {
            // Try absolute fallback if we know we are on user's machine
            if (File.Exists(@"e:\Cs\Nalix\shared\certificate.private"))
            {
                _certPath = @"e:\Cs\Nalix\shared\certificate.private";
            }
        }

        if (_certPath == null)
        {
            throw new FileNotFoundException("Could not find certificate.private in any parent directory.");
        }

        // Load the public key corresponding to the private key in certificate.private
        // HandshakeHandlers uses the private key to sign/agreement.
        // The client needs the PUBLIC key.
        // Since TestUtils.SetupCertificate() generates a fixed pair (for testing) 
        // or we can just read it if we know the format.

        // HandshakeHandlers.SetCertificatePath(_certPath);

        // Load the public key from certificate.public
        _serverPublicKey = Bytes32.Parse(TestUtils.GetServerPublicKey());

        // Initialize HandshakeHandlers with the private key path
        HandshakeHandlers.SetCertificatePath(_certPath);
    }

    private static string READ_HEX_FROM_FILE(string path)
    {
        string[] lines = File.ReadAllLines(path);
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }
            return trimmed;
        }
        throw new InvalidOperationException($"No hex found in {path}");
    }

    [Fact]
    public async Task HandshakeAsync_FullFlow_Succeeds()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.BindTcp<IntegrationTestProtocol>().OnPort((ushort)port);

        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            using TcpSession session = new(new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port,
                EncryptionEnabled = false,
                ServerPublicKey = _serverPublicKey.ToString()
            });

            await session.ConnectAsync();

            // Perform Handshake
            await session.HandshakeAsync();

            // Verify
            Assert.True(session.Options.EncryptionEnabled);
            Assert.NotEqual(Bytes32.Zero, session.Options.Secret);
            Assert.Equal(CipherSuiteType.Chacha20Poly1305, session.Options.Algorithm);
            Assert.NotEqual(0UL, session.Options.SessionToken);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    [Fact]
    public async Task ConnectWithResumeAsync_FullCycle_Succeeds()
    {
        int port = TestUtils.GetFreePort();
        var builder = NetworkApplication.CreateBuilder();
        builder.Configure<Nalix.Runtime.Options.SessionStoreOptions>(opt =>
        {
            opt.MinAttributesForPersistence = 0;
        });
        builder.BindTcp<IntegrationTestProtocol>().OnPort((ushort)port);

        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        try
        {
            using TcpSession session = new(new TransportOptions
            {
                Address = "127.0.0.1",
                Port = (ushort)port,
                EncryptionEnabled = false,
                ServerPublicKey = _serverPublicKey.ToString(),
                ResumeEnabled = true
            });

            // 1. First connect (performs Handshake)
            bool resumed1 = await session.ConnectWithResumeAsync();
            Assert.False(resumed1);
            Assert.NotEqual(0UL, session.Options.SessionToken);

            ulong token = session.Options.SessionToken;
            Bytes32 secret = session.Options.Secret;

            await session.DisconnectAsync();

            // Give the server a moment to background-persist the session snapshot
            // after it detects the TCP disconnect.
            await Task.Delay(200);

            // 2. Second connect (should resume)
            bool resumed2 = await session.ConnectWithResumeAsync();
            Assert.True(resumed2);
            
            Assert.NotEqual(0UL, session.Options.SessionToken);
            Assert.Equal(secret, session.Options.Secret);
            Assert.True(session.Options.EncryptionEnabled);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    public void Dispose() => InstanceManager.Instance.Clear(dispose: false);
}















