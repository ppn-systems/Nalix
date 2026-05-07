// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Contracts;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Tasks;
using Nalix.Network.RateLimiting;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Pooling;

namespace Backend.Handlers;

[PacketController("ExampleGenerationReport")]
public sealed class GenerationReportHandlers
{
    [PacketEncryption(true)]
    [PacketPermission(PermissionLevel.SYSTEM_ADMINISTRATOR)]
    [PacketOpcode(GenerationReport.OpCodeValue)]
    public static ValueTask<GenerationReport> HandleAsync(IPacketContext<GenerationReport> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GenerationReport request = context.Packet;

        if (request.Stage != GenerationReportStage.REQUEST || !request.Validate(out _))
        {
            return CreateResponse(request.Target, ProtocolReason.MALFORMED_PACKET);
        }

        if (!TryResolveReportable(request.Target, out IReportable? reportable))
        {
            return CreateResponse(request.Target, ProtocolReason.NOT_FOUND);
        }

        string dataJson = SerializeReportData(reportable!);

        return CreateResponse(request.Target, ProtocolReason.NONE, dataJson);
    }

    private static ValueTask<GenerationReport> CreateResponse(
        GenerationReportTarget target,
        ProtocolReason reason,
        string? dataJson = null)
    {
        PacketScope<GenerationReport> lease = PacketFactory<GenerationReport>.Acquire();

        try
        {
            GenerationReport response = lease.Value;
            response.Initialize(GenerationReportStage.RESPONSE, target, reason, dataJson);
            return ValueTask.FromResult(response);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static bool TryResolveReportable(GenerationReportTarget target, out IReportable? reportable)
    {
        InstanceManager instances = InstanceManager.Instance;

        reportable = target switch
        {
            GenerationReportTarget.INSTANCES => instances,
            GenerationReportTarget.NONE => throw new NotImplementedException(),
            GenerationReportTarget.TASKS => instances.GetExistingInstance<TaskManager>(),
            GenerationReportTarget.DISPATCH => instances.GetExistingInstance<IPacketDispatch>(),
            GenerationReportTarget.BUFFERS => instances.GetExistingInstance<BufferPoolManager>(),
            GenerationReportTarget.CONNECTIONS => instances.GetExistingInstance<IConnectionHub>(),
            GenerationReportTarget.OBJECT_POOLS => instances.GetExistingInstance<ObjectPoolManager>(),
            GenerationReportTarget.CONNECTION_GUARD => instances.GetExistingInstance<ConnectionGuard>(),
            _ => null
        };

        return reportable is not null;
    }

    private static string SerializeReportData(IReportable reportable)
    {
        ArrayBufferWriter<byte> bufferWriter = new(1024);
        using Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = true
        });

        reportable.WriteReportData(writer);
        writer.Flush();

        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
    }
}
