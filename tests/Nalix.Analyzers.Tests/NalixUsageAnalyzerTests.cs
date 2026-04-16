using Nalix.Analyzers.CodeFixes;

namespace Nalix.Analyzers.Tests;

public sealed class NalixUsageAnalyzerTests
{
    [Fact]
    public async Task PacketDeserializeSpanOverloadMissing_ReportsNalix052()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class MissingSpanPacket : PacketBase<MissingSpanPacket>
{
    public static MissingSpanPacket Deserialize(byte[] buffer) => PacketBase<MissingSpanPacket>.Deserialize(buffer);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX052");
    }

    [Fact]
    public async Task UtilityDeserializeMethods_DoNotReportPacketDiagnostic()
    {
        const string source = """
namespace Demo;

public static class DeserializeUtility
{
    public static int Deserialize(byte[] buffer, out int bytesRead)
    {
        bytesRead = buffer.Length;
        return bytesRead;
    }

    public static int Deserialize(ReadOnlyMemory<byte> buffer, out int bytesRead)
    {
        bytesRead = buffer.Length;
        return bytesRead;
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source);
    }

    [Fact]
    public async Task ResetForPoolWithoutBaseCall_CodeFixAddsBaseInvocation()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class MyPacket : PacketBase<MyPacket>
{
    public override void ResetForPool()
    {
    }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class MyPacket : PacketBase<MyPacket>
{
    public override void ResetForPool()
    {
        base.ResetForPool();
    }
}
""";

        await AnalyzerTestHarness.AssertCodeFixAsync<ResetForPoolCodeFixProvider>(
            source,
            fixedSource,
            diagnosticId: "NALIX020");
    }

    [Fact]
    public async Task PacketBaseSelfTypeMismatch_CodeFixUsesContainingType()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class WrongPacket : PacketBase<OtherPacket>
{
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class WrongPacket : PacketBase<WrongPacket>
{
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
}
""";

        await AnalyzerTestHarness.AssertCodeFixAsync<PacketSelfTypeCodeFixProvider>(
            source,
            fixedSource,
            diagnosticId: "NALIX010");
    }

    [Fact]
    public async Task PacketDeserializerSelfTypeMismatch_CodeFixUsesContainingType()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class WrongPacket : PacketBase<WrongPacket>, IPacketDeserializer<OtherPacket>
{
    public static new WrongPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<WrongPacket>.Deserialize(buffer);
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
    public static new OtherPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<OtherPacket>.Deserialize(buffer);
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class WrongPacket : PacketBase<WrongPacket>, IPacketDeserializer<WrongPacket>
{
    public static new WrongPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<WrongPacket>.Deserialize(buffer);
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
    public static new OtherPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<OtherPacket>.Deserialize(buffer);
}
""";

        await AnalyzerTestHarness.AssertCodeFixAsync<PacketSelfTypeCodeFixProvider>(
            source,
            fixedSource,
            diagnosticId: "NALIX011");
    }

    [Fact]
    public async Task RequestOptionsWithNegativeRetry_ReportsNalix027()
    {
        const string source = """
namespace Demo;
using Nalix.SDK.Options;

public static class RequestOptionUsage
{
    public static void Build()
    {
        _ = RequestOptions.Default.WithRetry(-1);
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX027");
    }

    [Fact]
    public async Task RequestOptionsWithNegativeTimeout_ReportsNalix028()
    {
        const string source = """
namespace Demo;
using Nalix.SDK.Options;

public static class RequestOptionUsage
{
    public static void Build()
    {
        _ = RequestOptions.Default.WithTimeout(-10);
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX028");
    }

    [Fact]
    public async Task PacketOpcodeOnNonControllerType_ReportsNalix050()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public sealed class NotAController
{
    [PacketOpcode(0x1234)]
    public void Handle(DemoPacket packet, IConnection connection)
    {
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX050");
    }

    [Fact]
    public async Task RequestAsync_InfiniteTimeoutWithRetry_ReportsNalix057()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public static class RequestUsage
{
    public static void Call(IClientConnection client, DemoPacket packet)
    {
        RequestOptions options = RequestOptions.Default
            .WithTimeout(0)
            .WithRetry(2);

        _ = client.RequestAsync<DemoPacket>(packet, options);
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX057");
    }

    [Fact]
    public async Task DispatchLoopCountOutOfRange_ReportsNalix047()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public static class DispatchSetup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<DemoPacket>().WithDispatchLoopCount(0);
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX047");
    }

    [Fact]
    public async Task MiddlewareRegistrationNullLiteral_ReportsNalix056()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Middleware;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public static class DispatchSetup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<DemoPacket>().WithMiddleware(null!);
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX056");
    }

    [Fact]
    public async Task HandlerControllerMissingPacketControllerAttribute_ReportsNalix008()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public sealed class PlainController
{
    [PacketOpcode(0x1200)]
    public void Handle(DemoPacket packet, IConnection connection)
    {
    }
}

public static class DispatchSetup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<DemoPacket>().WithHandler<PlainController>();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX008");
    }

    [Fact]
    public async Task UnsupportedConfigurationPropertyType_ReportsNalix023()
    {
        const string source = """
namespace Demo;
using System.Collections.Generic;
using Nalix.Framework.Configuration.Binding;

public sealed class DemoConfig : ConfigurationLoader
{
    public List<int> Values { get; set; } = new();
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX023");
    }

    [Fact]
    public async Task ConfigurationPropertyWithoutPublicSetter_ReportsNalix024()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.Configuration.Binding;

public sealed class DemoConfig : ConfigurationLoader
{
    public int Port { get; private set; } = 7777;
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX024");
    }

    [Fact]
    public async Task RequestAsyncEncryptInlineOnNonTcpClient_ReportsNalix029()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public static class RequestUsage
{
    public static void Call(IClientConnection client, DemoPacket packet)
    {
        _ = client.RequestAsync<DemoPacket>(packet, RequestOptions.Default.WithEncrypt(true));
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX029");
    }

    [Fact]
    public async Task RequestAsyncEncryptVariableOnNonTcpClient_ReportsNalix053()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public static class RequestUsage
{
    public static void Call(IClientConnection client, DemoPacket packet)
    {
        RequestOptions options = RequestOptions.Default.WithEncrypt(true);
        _ = client.RequestAsync<DemoPacket>(packet, options);
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX053");
    }

    [Fact]
    public async Task ReservedOpcodeInController_ReportsNalix035()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

[PacketController]
public sealed class DemoController
{
    [PacketOpcode(0x000A)]
    public void Handle(DemoPacket packet, IConnection connection)
    {
    }
}

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX035");
    }

    [Fact]
    public async Task PacketMiddlewareMissingOrder_ReportsNalix030()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Middleware;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public sealed class DemoPacketMiddleware : IPacketMiddleware<DemoPacket>
{
    public ValueTask InvokeAsync(IPacketContext<DemoPacket> context, Func<CancellationToken, ValueTask> next)
        => next(context.CancellationToken);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX030");
    }

    [Fact]
    public async Task BufferMiddlewareMissingOrder_ReportsNalix031()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Runtime.Middleware;

public sealed class DemoBufferMiddleware : INetworkBufferMiddleware
{
    public ValueTask<IBufferLease?> InvokeAsync(
        IBufferLease buffer,
        IConnection connection,
        Func<IBufferLease, CancellationToken, ValueTask<IBufferLease?>> nextHandler,
        CancellationToken ct) => nextHandler(buffer, ct);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX031");
    }

    [Fact]
    public async Task ControllerWithDuplicateOpcodes_ReportsNalix001()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

[PacketController]
public sealed class DemoController
{
    [PacketOpcode(0x1201)]
    public void HandleA(DemoPacket packet, IConnection connection) { }

    [PacketOpcode(0x1201)]
    public void HandleB(DemoPacket packet, IConnection connection) { }
}

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX001", "NALIX001");
    }

    [Fact]
    public async Task ControllerCandidateMethodMissingOpcode_ReportsNalix002()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

[PacketController]
public sealed class DemoController
{
    public void Handle(DemoPacket packet, IConnection connection) { }
}

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX002");
    }

    [Fact]
    public async Task ControllerHandlerInvalidSignature_ReportsNalix003()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

[PacketController]
public sealed class DemoController
{
    [PacketOpcode(0x1202)]
    public void Handle(DemoPacket packet) { }
}

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX003");
    }

    [Fact]
    public async Task WithMiddlewarePacketTypeMismatch_ReportsNalix006()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Middleware;
using Nalix.Common.Middleware;

public sealed class PacketA : PacketBase<PacketA>
{
    public static new PacketA Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<PacketA>.Deserialize(buffer);
}

public sealed class PacketB : PacketBase<PacketB>
{
    public static new PacketB Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<PacketB>.Deserialize(buffer);
}

[MiddlewareOrder(1)]
public sealed class PacketBMiddleware : IPacketMiddleware<PacketB>
{
    public ValueTask InvokeAsync(IPacketContext<PacketB> context, Func<CancellationToken, ValueTask> next) => next(context.CancellationToken);
}

public static class Setup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<PacketA>().WithMiddleware(new PacketBMiddleware());
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX006");
    }

    [Fact]
    public async Task BufferMiddlewareWithStageAttribute_ReportsNalix007()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Abstractions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Runtime.Middleware;

[MiddlewareOrder(1)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class DemoBufferMiddleware : INetworkBufferMiddleware
{
    public ValueTask<IBufferLease?> InvokeAsync(
        IBufferLease buffer,
        IConnection connection,
        Func<IBufferLease, CancellationToken, ValueTask<IBufferLease?>> nextHandler,
        CancellationToken ct) => nextHandler(buffer, ct);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX007");
    }

    [Fact]
    public async Task RegisterPacketAbstractType_ReportsNalix018()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public abstract class AbstractPacket : IPacket { }

public static class Setup
{
    public static void Configure()
    {
        _ = new PacketRegistryFactory().RegisterPacket<AbstractPacket>();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX018");
    }

    [Fact]
    public async Task RegisterPacketWithoutDeserializer_ReportsNalix009()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class PlainPacket : IPacket { }

public static class Setup
{
    public static void Configure()
    {
        _ = new PacketRegistryFactory().RegisterPacket<PlainPacket>();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX009");
    }

    [Fact]
    public async Task ExplicitSerializeMemberMissingOrder_ReportsNalix013()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class ExplicitPacket
{
    public int A { get; set; }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX013");
    }

    [Fact]
    public async Task DuplicateSerializeOrder_ReportsNalix014()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class ExplicitPacket
{
    [SerializeOrder(1)]
    public int A { get; set; }

    [SerializeOrder(1)]
    public int B { get; set; }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX014", "NALIX014");
    }

    [Fact]
    public async Task SerializeIgnoreWithOrder_ReportsNalix015()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class ExplicitPacket
{
    [SerializeIgnore]
    [SerializeOrder(2)]
    public int A { get; set; }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX015");
    }

    [Fact]
    public async Task SerializeDynamicSizeOnFixedType_ReportsNalix016()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class ExplicitPacket
{
    [SerializeOrder(1)]
    [SerializeDynamicSize]
    public int A { get; set; }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX016");
    }

    [Fact]
    public async Task NegativeSerializeOrder_ReportsNalix021()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class ExplicitPacket
{
    [SerializeOrder(-1)]
    public int A { get; set; }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX021");
    }

    [Fact]
    public async Task MetadataProviderClearsOpcode_ReportsNalix025()
    {
        const string source = """
namespace Demo;
using System.Reflection;
using Nalix.Runtime.Dispatching;

public sealed class Provider : IPacketMetadataProvider
{
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        builder.Opcode = null;
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX025");
    }

    [Fact]
    public async Task MetadataProviderOverwritesOpcodeWithoutGuard_ReportsNalix026()
    {
        const string source = """
namespace Demo;
using System.Reflection;
using Nalix.Common.Networking.Packets;
using Nalix.Runtime.Dispatching;

public sealed class Provider : IPacketMetadataProvider
{
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        builder.Opcode = new PacketOpcodeAttribute(0x1210);
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX026");
    }

    [Fact]
    public async Task InboundAlwaysExecuteOnMiddleware_ReportsNalix032()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Middleware;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[MiddlewareOrder(1)]
[MiddlewareStage(MiddlewareStage.Inbound, AlwaysExecute = true)]
public sealed class DemoMiddleware : IPacketMiddleware<DemoPacket>
{
    public ValueTask InvokeAsync(IPacketContext<DemoPacket> context, Func<CancellationToken, ValueTask> next) => next(context.CancellationToken);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX032");
    }

    [Fact]
    public async Task DuplicateMiddlewareOrderInChain_ReportsNalix033()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Middleware;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[MiddlewareOrder(5)]
public sealed class Mw1 : IPacketMiddleware<DemoPacket>
{
    public ValueTask InvokeAsync(IPacketContext<DemoPacket> context, Func<CancellationToken, ValueTask> next) => next(context.CancellationToken);
}

[MiddlewareOrder(5)]
public sealed class Mw2 : IPacketMiddleware<DemoPacket>
{
    public ValueTask InvokeAsync(IPacketContext<DemoPacket> context, Func<CancellationToken, ValueTask> next) => next(context.CancellationToken);
}

public static class Setup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<DemoPacket>()
            .WithMiddleware(new Mw1())
            .WithMiddleware(new Mw2());
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX033");
    }

    [Fact]
    public async Task SerializeHeaderConflictsWithOrder_ReportsNalix034()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class ExplicitPacket
{
    [SerializeHeader(1)]
    [SerializeOrder(1)]
    public int A { get; set; }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX034");
    }

    [Fact]
    public async Task GlobalDuplicateOpcodeAcrossControllers_ReportsNalix036()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[PacketController]
public sealed class ControllerA
{
    [PacketOpcode(0x1234)]
    public void Handle(DemoPacket packet, IConnection connection) { }
}

[PacketController]
public sealed class ControllerB
{
    [PacketOpcode(0x1234)]
    public void Handle(DemoPacket packet, IConnection connection) { }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX036");
    }

    [Fact]
    public async Task AllocationInHotPath_ReportsNalix037()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class Helper { }

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[PacketController]
public sealed class ControllerA
{
    [PacketOpcode(0x1240)]
    public void Handle(DemoPacket packet, IConnection connection)
    {
        _ = new Helper();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX037");
    }

    [Fact]
    public async Task OpCodeDocumentationMismatch_ReportsNalix038()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[PacketController]
public sealed class ControllerA
{
    /// <summary>Handles packet opcode 0x8888.</summary>
    [PacketOpcode(0x1241)]
    public void Handle(DemoPacket packet, IConnection connection) { }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX038");
    }

    [Fact]
    public async Task BufferLeaseParameterNotDisposed_ReportsNalix039()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Abstractions;

public sealed class LeakDemo
{
    public void Process(IBufferLease lease)
    {
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX039");
    }

    [Fact]
    public async Task UnusuallyLargeSerializeOrderGap_ReportsNalix046()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class ExplicitPacket
{
    [SerializeOrder(1)]
    public int A { get; set; }

    [SerializeOrder(20)]
    public int B { get; set; }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX046");
    }

    [Fact]
    public async Task ControllerUnsupportedReturnType_ReportsNalix048()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[PacketController]
public sealed class ControllerA
{
    [PacketOpcode(0x1242)]
    public int Handle(DemoPacket packet, IConnection connection) => 1;
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX048");
    }

    [Fact]
    public async Task FixedSizeSerializableWithDynamicMember_ReportsNalix051()
    {
        const string source = """
namespace Nalix.Common.Serialization
{
    public interface IFixedSizeSerializable { }
}

namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class FixedType : IFixedSizeSerializable
{
    [SerializeOrder(1)]
    public string Name { get; set; } = string.Empty;
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX051");
    }

    [Fact]
    public async Task DuplicatePacketControllerName_ReportsNalix054()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[PacketController("dup")]
public sealed class ControllerA
{
    [PacketOpcode(0x1243)]
    public void Handle(DemoPacket packet, IConnection connection) { }
}

[PacketController("dup")]
public sealed class ControllerB
{
    [PacketOpcode(0x1244)]
    public void Handle(DemoPacket packet, IConnection connection) { }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX054");
    }

    [Fact]
    public async Task RedundantPacketContextPacketCast_ReportsNalix055()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[PacketController]
public sealed class ControllerA
{
    [PacketOpcode(0x1245)]
    public void Handle(PacketContext<DemoPacket> context)
    {
        _ = (DemoPacket)context.Packet;
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX055");
    }

    [Fact]
    public async Task GenericPacketHandlerMethod_ReportsNalix058()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

[PacketController]
public sealed class ControllerA
{
    [PacketOpcode(0x1246)]
    public void Handle<T>(DemoPacket packet, IConnection connection) { }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX058");
    }

    [Fact]
    public async Task DispatchLoopCountBoundary_DoesNotReportNalix047()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public static class DispatchSetup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<DemoPacket>()
            .WithDispatchLoopCount(1)
            .WithDispatchLoopCount(64);
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source);
    }

    [Fact]
    public async Task WithHandlerPacketContextMismatch_ReportsNalix004()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
    public static new OtherPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<OtherPacket>.Deserialize(buffer);
}

[PacketController]
public sealed class DemoController
{
    [PacketOpcode(0x1250)]
    public void Handle(PacketContext<OtherPacket> context) { }
}

public static class Setup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<DemoPacket>().WithHandler<DemoController>();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX004");
    }

    [Fact]
    public async Task WithHandlerLegacyPacketMismatch_ReportsNalix005()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Runtime.Dispatching;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public sealed class OtherPacket : PacketBase<OtherPacket>
{
    public static new OtherPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<OtherPacket>.Deserialize(buffer);
}

[PacketController]
public sealed class DemoController
{
    [PacketOpcode(0x1251)]
    public void Handle(OtherPacket packet, IConnection connection) { }
}

public static class Setup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<DemoPacket>().WithHandler<DemoController>();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX005");
    }

    [Fact]
    public async Task WithBufferMiddlewareTypeMismatch_ReportsNalix019()
    {
        const string source = """
namespace Demo;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;

public sealed class DemoPacket : PacketBase<DemoPacket>
{
    public static new DemoPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<DemoPacket>.Deserialize(buffer);
}

public sealed class NotABufferMiddleware
{
}

public static class Setup
{
    public static void Configure()
    {
        _ = new PacketDispatchOptions<DemoPacket>()
            .WithBufferMiddleware((INetworkBufferMiddleware)(object)new NotABufferMiddleware());
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX019");
    }

    [Fact]
    public async Task NetworkBuildWithoutRecommendedSetup_ReportsNalix040_041_044()
    {
        const string source = """
namespace Demo;
using Nalix.Network.Hosting;

public static class Setup
{
    public static void Configure()
    {
        _ = new NetworkApplicationBuilder().Build();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX040", "NALIX041", "NALIX044");
    }

    [Fact]
    public async Task NetworkBuildWithUdpWithoutTcp_ReportsNalix045()
    {
        const string source = """
namespace Demo;
using Nalix.Network.Hosting;

public static class Setup
{
    public static void Configure()
    {
        _ = new NetworkApplicationBuilder()
            .AddUdp(9000)
            .Build();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX040", "NALIX041", "NALIX044", "NALIX045");
    }

    [Fact]
    public async Task AddHandlerWithInvalidType_ReportsNalix042()
    {
        const string source = """
namespace Demo;
using Nalix.Network.Hosting;

public interface IHandler { }

public static class Setup
{
    public static void Configure()
    {
        _ = new NetworkApplicationBuilder().AddHandler<IHandler>();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX042");
    }

    [Fact]
    public async Task AddMetadataProviderWithInvalidType_ReportsNalix043()
    {
        const string source = """
namespace Demo;
using Nalix.Network.Hosting;

public interface IProvider { }

public static class Setup
{
    public static void Configure()
    {
        _ = new NetworkApplicationBuilder().AddMetadataProvider<IProvider>();
    }
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX043");
    }

    [Fact]
    public async Task PacketBaseMissingDeserializeMethod_ReportsNalix012()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class MissingDeserializePacket : PacketBase<MissingDeserializePacket>
{
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX012");
    }

    [Fact]
    public async Task PacketDeserializeSignatureInvalid_ReportsNalix017()
    {
        const string source = """
namespace Demo;
using Nalix.Framework.DataFrames;

public sealed class BadDeserializePacket : PacketBase<BadDeserializePacket>
{
    public static int Deserialize(ReadOnlySpan<byte> buffer) => 0;
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX017", "NALIX052");
    }

    [Fact]
    public async Task PacketMemberOverlapsHeaderRegion_ReportsNalix022()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class HeaderOverlapPacket : PacketBase<HeaderOverlapPacket>
{
    [SerializeOrder(1)]
    public int Dangerous { get; set; }

    public static new HeaderOverlapPacket Deserialize(ReadOnlySpan<byte> buffer) => PacketBase<HeaderOverlapPacket>.Deserialize(buffer);
}
""";

        await AnalyzerTestHarness.AssertDiagnosticIdsAsync(source, "NALIX022");
    }
}
