// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Environment.Memory;
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
        Console.WriteLine($"[RuntimeObservationHandlers] Received request. Stage={request.Stage}, Target={request.Target}");

        string? failureReason = null;
        if (request.Stage != RuntimeObservationStage.REQUEST || !request.Validate(out failureReason))
        {
            Console.WriteLine($"[RuntimeObservationHandlers] Validation failed: {failureReason ?? "Stage is not REQUEST"}");
            return CreateResponse(request.Target, ProtocolReason.MALFORMED_PACKET);
        }

        if (!TryResolveReportable(request.Target, out IReportable? reportable))
        {
            Console.WriteLine($"[RuntimeObservationHandlers] Target reportable not found: {request.Target}");
            return CreateResponse(request.Target, ProtocolReason.NOT_FOUND);
        }

        try
        {
            BufferLease lease = SerializeReportData(reportable!);
            Console.WriteLine($"[RuntimeObservationHandlers] Successfully serialized report for {request.Target}. Length={lease.Length} bytes.");
            return CreateResponse(request.Target, ProtocolReason.NONE, lease);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RuntimeObservationHandlers] Error serializing report: {ex}");
            throw;
        }
    }

    private static ValueTask<RuntimeObservation> CreateResponse(
        RuntimeObservationTarget target,
        ProtocolReason reason,
        BufferLease? bufferLease = null)
    {
        PacketScope<RuntimeObservation> lease = PacketFactory<RuntimeObservation>.Acquire();

        try
        {
            RuntimeObservation response = lease.Value;
            response.Initialize(RuntimeObservationStage.RESPONSE, target, reason, ObservationData: default);
            if (bufferLease is not null)
            {
                response.AssociateLease(bufferLease);
            }
            return ValueTask.FromResult(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RuntimeObservationHandlers] Exception in CreateResponse: {ex}");
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

    private static BufferLease SerializeReportData(IReportable reportable)
    {
        Console.WriteLine($"[RuntimeObservationHandlers] Serializing reportable {reportable.GetType().Name}...");
        using PooledBufferWriter bufferWriter = new(1024 * 8);
        try
        {
            using (Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions
            {
                Indented = false,
                SkipValidation = true
            }))
            {
                reportable.WriteReportData(writer);
                writer.Flush();
            }

            BufferLease lease = bufferWriter.ExtractLease();
            Console.WriteLine($"[RuntimeObservationHandlers] Extracted lease shell. Span size: {lease.Length}");
            return lease;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RuntimeObservationHandlers] Exception in SerializeReportData: {ex}");
            throw;
        }
    }
}
