// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Network.RateLimiting;
using Nalix.Observability.Contracts;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Pooling;

namespace Nalix.Observability.Handlers;

[PacketController("Nalix.RuntimeObservation")]
public sealed class RuntimeObservationHandlers
{
    [PacketEncryption(true)]
    [PacketOpcode(RuntimeObservation.OpCodeValue)]
    [PacketPermission(PermissionLevel.SYSTEM_ADMINISTRATOR)]
    public static ValueTask<RuntimeObservation> HandleAsync(IPacketContext<RuntimeObservation> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        RuntimeObservation request = context.Packet;

        if (request.Stage != RuntimeObservationStage.REQUEST || !request.Validate(out _))
        {
            return CreateResponse(request.Target, ProtocolReason.MALFORMED_PACKET);
        }

        if (!TryResolveReportable(request.Target, out IReportable? reportable))
        {
            return CreateResponse(request.Target, ProtocolReason.NOT_FOUND);
        }

        string ObservationData = SerializeReportData(reportable!);

        return CreateResponse(request.Target, ProtocolReason.NONE, ObservationData);
    }

    private static ValueTask<RuntimeObservation> CreateResponse(
        RuntimeObservationTarget target,
        ProtocolReason reason,
        string? ObservationData = null)
    {
        PacketScope<RuntimeObservation> lease = PacketFactory<RuntimeObservation>.Acquire();

        try
        {
            RuntimeObservation response = lease.Value;
            response.Initialize(RuntimeObservationStage.RESPONSE, target, reason, ObservationData);
            return ValueTask.FromResult(response);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static bool TryResolveReportable(RuntimeObservationTarget target, out IReportable? reportable)
    {
        InstanceManager instances = InstanceManager.Instance;

        reportable = target switch
        {
            RuntimeObservationTarget.INSTANCES => instances,
            RuntimeObservationTarget.NONE => throw new NotImplementedException(),
            RuntimeObservationTarget.TASKS => instances.GetExistingInstance<TaskManager>(),
            RuntimeObservationTarget.DISPATCH => instances.GetExistingInstance<IPacketDispatch>(),
            RuntimeObservationTarget.BUFFERS => instances.GetExistingInstance<IBufferPoolManager>(),
            RuntimeObservationTarget.CONNECTIONS => instances.GetExistingInstance<IConnectionHub>(),
            RuntimeObservationTarget.OBJECT_POOLS => instances.GetExistingInstance<IObjectPoolManager>(),
            RuntimeObservationTarget.CONNECTION_GUARD => instances.GetExistingInstance<ConnectionGuard>(),
            _ => null
        };

        return reportable is not null;
    }

    private static string SerializeReportData(IReportable reportable)
    {
        ArrayBufferWriter<byte> bufferWriter = new(1024 * 8);
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

