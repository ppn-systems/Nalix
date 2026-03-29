// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Analyzers.CodeFixes;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class MiddlewareAnalyzerTests
{
    [Fact]
    public async Task PacketMiddleware_WithoutOrder_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

public sealed class DemoMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}
""";

        await Verifier<MiddlewareCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX030");
    }

    [Fact]
    public async Task PacketMiddleware_WithoutOrder_CanAddOrderFix()
    {
        const string source = """
namespace Demo;
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

public sealed class DemoMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}
""";

        const string fixedSource = """
namespace Demo;
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

[MiddlewareOrder(0)]
public sealed class DemoMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}
""";

        await Verifier<MiddlewareCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX030",
            actionIndex: 0,
            expectedTitle: "Add [MiddlewareOrder(0)]",
            expectedEquivalenceKey: "Nalix.Middleware.Order.AddDefault");
    }

    [Fact]
    public async Task BufferMiddleware_WithoutOrder_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Network.Middleware;

public sealed class DemoBufferMiddleware : INetworkBufferMiddleware
{
    public Task<IBufferLease?> InvokeAsync(IBufferLease buffer, IConnection connection, Func<IBufferLease, CancellationToken, Task<IBufferLease?>> nextHandler, CancellationToken ct)
        => nextHandler(buffer, ct);
}
""";

        await Verifier<MiddlewareCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX031");
    }

    [Fact]
    public async Task BufferMiddleware_WithoutOrder_CanAddOrderFix()
    {
        const string source = """
namespace Demo;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Network.Middleware;

public sealed class DemoBufferMiddleware : INetworkBufferMiddleware
{
    public Task<IBufferLease?> InvokeAsync(IBufferLease buffer, IConnection connection, Func<IBufferLease, CancellationToken, Task<IBufferLease?>> nextHandler, CancellationToken ct)
        => nextHandler(buffer, ct);
}
""";

        const string fixedSource = """
namespace Demo;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Network.Middleware;

[MiddlewareOrder(0)]
public sealed class DemoBufferMiddleware : INetworkBufferMiddleware
{
    public Task<IBufferLease?> InvokeAsync(IBufferLease buffer, IConnection connection, Func<IBufferLease, CancellationToken, Task<IBufferLease?>> nextHandler, CancellationToken ct)
        => nextHandler(buffer, ct);
}
""";

        await Verifier<MiddlewareCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX031",
            actionIndex: 0,
            expectedTitle: "Add [MiddlewareOrder(0)]",
            expectedEquivalenceKey: "Nalix.Middleware.Order.AddDefault");
    }

    [Fact]
    public async Task InboundMiddleware_WithAlwaysExecute_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

[MiddlewareOrder(0)]
[MiddlewareStage(MiddlewareStage.Inbound, AlwaysExecute = true)]
public sealed class DemoMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}
""";

        await Verifier<MiddlewareCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX032");
    }

    [Fact]
    public async Task InboundMiddleware_WithAlwaysExecute_CanRemoveFlagFix()
    {
        const string source = """
namespace Demo;
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

[MiddlewareOrder(0)]
[MiddlewareStage(MiddlewareStage.Inbound, AlwaysExecute = true)]
public sealed class DemoMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}
""";

        const string fixedSource = """
namespace Demo;
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

[MiddlewareOrder(0)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class DemoMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}
""";

        await Verifier<MiddlewareCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX032",
            actionIndex: 0,
            expectedTitle: "Remove AlwaysExecute = true",
            expectedEquivalenceKey: "Nalix.Middleware.Stage.RemoveAlwaysExecute");
    }

    [Fact]
    public async Task PacketMiddleware_WithOrder_IsSilent()
    {
        const string source = """
namespace Demo;
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

[MiddlewareOrder(10)]
public sealed class DemoMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}
""";

        await Verifier<MiddlewareCodeFixProvider>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task WithMiddleware_DuplicateOrderInChain_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Routing;

public sealed class LoginPacket : Nalix.Framework.DataFrames.PacketBase<LoginPacket>
{
    public static new LoginPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<LoginPacket>.Deserialize(buffer);
}

[MiddlewareOrder(10)]
public sealed class FirstMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}

[MiddlewareOrder(10)]
public sealed class SecondMiddleware : IPacketMiddleware<LoginPacket>
{
    public Task InvokeAsync(PacketContext<LoginPacket> context, Func<CancellationToken, Task> next) => next(CancellationToken.None);
}

public sealed class Example
{
    public void Run()
    {
        _ = new PacketDispatchOptions<LoginPacket>()
            .WithMiddleware(new FirstMiddleware())
            .WithMiddleware(new SecondMiddleware());
    }
}
""";

        await Verifier<MiddlewareCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX033");
    }
}
