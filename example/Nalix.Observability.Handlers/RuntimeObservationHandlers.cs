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
using Nalix.Observability.Handlers.Internal;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Pooling;

namespace Nalix.Observability.Handlers;

/// <summary>
/// Handles telemetry observation packets and returns serialized diagnostic reports.
/// </summary>
[PacketController("Nalix.RuntimeObservation")]
public sealed class RuntimeObservationHandlers
{
    private static readonly IReportable?[] s_reportableCache = new IReportable?[256];

    /// <summary>
    /// Handles an incoming runtime observation request.
    /// </summary>
    /// <param name="context">The packet context.</param>
    /// <returns>A value task representing the response packet.</returns>
    [PacketEncryption(true)]
    [PacketOpcode(RuntimeObservation.OpCodeValue)]
    [PacketPermission(PermissionLevel.SUPERVISOR)]
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

        BufferLease lease = SerializeReportData(reportable!);
        return CreateResponse(request.Target, ProtocolReason.NONE, lease);
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
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static bool TryResolveReportable(RuntimeObservationTarget target, out IReportable? reportable)
    {
        int index = (int)target;
        if (index >= 0 && index < s_reportableCache.Length)
        {
            IReportable? cached = s_reportableCache[index];
            if (cached is not null)
            {
                reportable = cached;
                return true;
            }
        }

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

        if (reportable is not null && index >= 0 && index < s_reportableCache.Length)
        {
            s_reportableCache[index] = reportable;
        }

        return reportable is not null;
    }

    private static BufferLease SerializeReportData(IReportable reportable)
    {
        using BufferWriter bufferWriter = new(1024 * 8);
        using (Utf8JsonWriter writer = new(bufferWriter, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = true
        }))
        {
            reportable.WriteReportData(writer);
            writer.Flush();
        }

        return bufferWriter.ExtractLease();
    }
}
