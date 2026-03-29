// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Analyzers.CodeFixes;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class RequestOptionsAnalyzerTests
{
    [Fact]
    public async Task WithRetry_NegativeValue_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.SDK.Configuration;

public sealed class Example
{
    public void Run()
    {
        _ = RequestOptions.Default.WithRetry(-1);
    }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX027");
    }

    [Fact]
    public async Task WithTimeout_NegativeValue_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.SDK.Configuration;

public sealed class Example
{
    public void Run()
    {
        _ = RequestOptions.Default.WithTimeout(-500);
    }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX028");
    }

    [Fact]
    public async Task RequestAsync_WithEncryptedOptionsOnNonTcpClient_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

public sealed class PlainClient : IClientConnection
{
    public ITransportOptions Options => null!;
    public bool IsConnected => true;
    public IPacketRegistry Catalog => null!;
    public event EventHandler OnConnected { add { } remove { } }
    public event EventHandler<Exception> OnDisconnected { add { } remove { } }
    public event EventHandler<Nalix.Common.Abstractions.IBufferLease> OnMessageReceived { add { } remove { } }
    public event EventHandler<long> OnBytesSent { add { } remove { } }
    public event EventHandler<long> OnBytesReceived { add { } remove { } }
    public event EventHandler<Exception> OnError { add { } remove { } }
    public Task ConnectAsync(string? host = null, ushort? port = null, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;
    public Task SendAsync(IPacket packet, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task SendAsync(ReadOnlyMemory<byte> payload, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() { }
}

public sealed class Example
{
    public Task<LoginPacket> Run(PlainClient client, LoginPacket packet)
        => client.RequestAsync<LoginPacket>(packet, RequestOptions.Default.WithEncrypt());
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX029");
    }

    [Fact]
    public async Task RequestAsync_WithEncryptedOptionsOnTcpSession_IsSilent()
    {
        const string source = """
namespace Demo;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

public sealed class TcpClient : TcpSessionBase
{
}

public sealed class Example
{
    public Task<LoginPacket> Run(TcpClient client, LoginPacket packet)
        => client.RequestAsync<LoginPacket>(packet, RequestOptions.Default.WithEncrypt());
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source);
    }
}
