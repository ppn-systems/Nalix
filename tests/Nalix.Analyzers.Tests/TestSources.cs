// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Analyzers.Tests;

internal static class TestSources
{
    public const string Prelude = """
global using System;
global using System.Threading;
global using System.Threading.Tasks;
global using Nalix.Framework.DataFrames;

namespace Nalix.Common.Serialization
{
    public enum SerializeLayout : byte { Sequential = 0, Explicit = 1 }
    public sealed class SerializePackableAttribute : Attribute { public SerializePackableAttribute(SerializeLayout layout) { } }
    public sealed class SerializeHeaderAttribute : Attribute { public SerializeHeaderAttribute(int order) { } }
    public sealed class SerializeOrderAttribute : Attribute { public SerializeOrderAttribute(int order) { } }
    public sealed class SerializeIgnoreAttribute : Attribute { }
    public sealed class SerializeDynamicSizeAttribute : Attribute { public SerializeDynamicSizeAttribute(int size = 0) { } }
}

namespace Nalix.Common.Networking.Packets
{
    public enum PacketHeaderOffset { MagicNumber = 0, OpCode = 4, Flags = 6, Priority = 7, Transport = 8, SequenceId = 9, Region = 12 }
    [Nalix.Common.Serialization.SerializePackable(Nalix.Common.Serialization.SerializeLayout.Explicit)]
    public interface IPacket { }
    public interface IPacketRegistry { }
    public interface IPacketDeserializer<TPacket> where TPacket : IPacket { }
    public sealed class PacketOpcodeAttribute : Attribute { public PacketOpcodeAttribute(ushort opcode) { } }
    public sealed class PacketControllerAttribute : Attribute { }
}

namespace Nalix.Common.Networking
{
    public interface IConnection { }
}

namespace Nalix.Common.Middleware
{
    public enum MiddlewareStage : byte { Inbound, Outbound, Both }
    public sealed class MiddlewareOrderAttribute : Attribute { public MiddlewareOrderAttribute(int order) { } }
    public sealed class MiddlewareStageAttribute : Attribute
    {
        public MiddlewareStageAttribute(MiddlewareStage stage) { }
        public bool AlwaysExecute { get; init; }
    }
}

namespace Nalix.Common.Abstractions
{
    public interface IBufferLease { }
    public sealed class ConfiguredIgnoreAttribute : Attribute
    {
        public ConfiguredIgnoreAttribute(string? reason = null) { }
    }
}

namespace Nalix.Runtime.Dispatching
{
    using System.Reflection;
    using Nalix.Common.Networking.Packets;
    using Nalix.Common.Networking;
    using Nalix.Runtime.Middleware;

    public sealed class PacketContext<TPacket> : IPacketContext<TPacket> where TPacket : IPacket
    {
        public TPacket Packet => default!;
        public IConnection Connection => null!;
        public CancellationToken CancellationToken => default;
    }
    public sealed class PacketDispatchOptions<TPacket> where TPacket : IPacket
    {
        public PacketDispatchOptions<TPacket> WithHandler<TController>() where TController : class => this;
        public PacketDispatchOptions<TPacket> WithMiddleware(IPacketMiddleware<TPacket> middleware) => this;
        public PacketDispatchOptions<TPacket> WithBufferMiddleware(INetworkBufferMiddleware middleware) => this;
    }

    public sealed class PacketMetadataBuilder
    {
        public PacketOpcodeAttribute? Opcode { get; set; }
        public void Add(Attribute attribute) { }
        public TAttribute? Get<TAttribute>() where TAttribute : Attribute => null;
    }

    public interface IPacketMetadataProvider
    {
        void Populate(MethodInfo method, PacketMetadataBuilder builder);
    }
}

namespace Nalix.Runtime.Middleware
{
    using Nalix.Common.Abstractions;
    using Nalix.Common.Networking;
    using Nalix.Common.Networking.Packets;
    using Nalix.Runtime.Dispatching;

    public interface IPacketMiddleware<TPacket> where TPacket : IPacket
    {
        ValueTask InvokeAsync(IPacketContext<TPacket> context, Func<CancellationToken, ValueTask> next);
    }

    public interface INetworkBufferMiddleware
    {
        ValueTask<IBufferLease?> InvokeAsync(IBufferLease buffer, IConnection connection, Func<IBufferLease, CancellationToken, ValueTask<IBufferLease?>> nextHandler, CancellationToken ct);
    }
}

namespace Nalix.Common.Networking.Packets
{
    public interface IPacketContext<TPacket> where TPacket : IPacket
    {
        TPacket Packet { get; }
        IConnection Connection { get; }
        CancellationToken CancellationToken { get; }
    }
}
namespace Nalix.Framework.DataFrames
{
    using Nalix.Common.Networking.Packets;

    public abstract class PacketBase<TSelf> : IPacket, IPacketDeserializer<TSelf> where TSelf : PacketBase<TSelf>
    {
        public virtual void ResetForPool() { }
        public static TSelf Deserialize(ReadOnlySpan<byte> buffer) => default!;
    }

    public sealed class PacketRegistryFactory
    {
        public PacketRegistryFactory RegisterPacket<TPacket>() where TPacket : IPacket => this;
    }
}

namespace Nalix.SDK.Transport
{
    using System.Threading;
    using System.Threading.Tasks;
    using Nalix.Common.Abstractions;
    using Nalix.Common.Networking.Packets;

    public interface ITransportOptions { }

    public interface IClientConnection : IDisposable
    {
        ITransportOptions Options { get; }
        bool IsConnected { get; }
        IPacketRegistry Catalog { get; }
        event EventHandler OnConnected;
        event EventHandler<Exception> OnDisconnected;
        event EventHandler<IBufferLease> OnMessageReceived;
        event EventHandler<long> OnBytesSent;
        event EventHandler<long> OnBytesReceived;
        event EventHandler<Exception> OnError;
        Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default);
        Task DisconnectAsync();
        Task SendAsync(IPacket packet, CancellationToken ct = default);
        Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default);
    }

    public abstract class TcpSession : IClientConnection
    {
        public ITransportOptions Options => null!;
        public bool IsConnected => true;
        public IPacketRegistry Catalog => null!;
        public event EventHandler OnConnected { add { } remove { } }
        public event EventHandler<Exception> OnDisconnected { add { } remove { } }
        public event EventHandler<IBufferLease> OnMessageReceived { add { } remove { } }
        public event EventHandler<long> OnBytesSent { add { } remove { } }
        public event EventHandler<long> OnBytesReceived { add { } remove { } }
        public event EventHandler<Exception> OnError { add { } remove { } }
        public Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task SendAsync(IPacket packet, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendAsync(IPacket packet, bool encrypt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }
}

namespace Nalix.SDK.Options
{
    public sealed record RequestOptions
    {
        public static RequestOptions Default { get; } = new();
        public int TimeoutMs { get; init; }
        public int RetryCount { get; init; }
        public bool Encrypt { get; init; }
        public RequestOptions WithTimeout(int ms) => this with { TimeoutMs = ms };
        public RequestOptions WithRetry(int count) => this with { RetryCount = count };
        public RequestOptions WithEncrypt(bool encrypt = true) => this with { Encrypt = encrypt };
    }
}

namespace Nalix.SDK.Transport.Extensions
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Nalix.Common.Networking.Packets;
    using Nalix.SDK.Options;
    using Nalix.SDK.Transport;

    public static class RequestExtensions
    {
        public static Task<TResponse> RequestAsync<TResponse>(
            this IClientConnection client,
            IPacket request,
            RequestOptions? options = null,
            Func<TResponse, bool>? predicate = null,
            CancellationToken ct = default)
            where TResponse : class, IPacket => Task.FromResult<TResponse>(null!);
    }
}

namespace Nalix.Framework.Configuration.Binding
{
    public abstract class ConfigurationLoader
    {
    }
}

namespace Nalix.Network.Hosting
{
    public sealed class NetworkApplicationBuilder
    {
        public NetworkApplicationBuilder UseBufferPoolManager(object? manager = null) => this;
        public NetworkApplicationBuilder ConfigureConnectionHub(object? hub = null) => this;
        public NetworkApplicationBuilder AddTcp(ushort port = 0) => this;
        public NetworkApplicationBuilder AddUdp(ushort port = 0) => this;
        public NetworkApplicationBuilder AddHandler<THandler>() => this;
        public NetworkApplicationBuilder AddMetadataProvider<TProvider>() => this;
        public object Build() => new();
    }
}
""";
}

