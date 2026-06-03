// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Nalix.Abstractions;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Environment.Memory;
using Nalix.Framework;
using Nalix.Framework.Injection;
using Nalix.Observability.Contracts;
using Nalix.Observability.Internal;
using Nalix.Codec.Pooling;

namespace Nalix.Observability;

/// <summary>
/// Handles telemetry observation packets and returns serialized diagnostic reports.
/// </summary>
[PacketController("Nalix.RuntimeObservation")]
public sealed class RuntimeObservationHandlers
{
    /// <summary>
    /// Handles an incoming runtime observation request.
    /// </summary>
    /// <param name="context">The packet context.</param>
    /// <returns>A value task representing the response packet.</returns>
    [PacketEncryption(true)]
    [ReservedOpcodePermitted]
    [PacketOpcode(RuntimeObservation.OpCodeValue)]
    [PacketPermission(PermissionLevel.SUPERVISOR)]
    public static ValueTask<RuntimeObservation> HandleAsync(IPacketContext<RuntimeObservation> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        RuntimeObservation request = context.Packet;

        if (request.Stage != RuntimeObservationStage.REQUEST || !request.Validate(out _))
        {
            return CREATE_RESPONSE(request.Target, ProtocolReason.MALFORMED_PACKET);
        }

        if (!TRY_RESOLVE_REPORTABLE(request.Target, out IReportable? reportable))
        {
            return CREATE_RESPONSE(request.Target, ProtocolReason.NOT_FOUND);
        }

        BufferLease lease = SERIALIZE_REPORT_DATA(reportable!);
        return CREATE_RESPONSE(request.Target, ProtocolReason.NONE, lease);
    }

    #region Private Methods

    private static BufferLease SERIALIZE_REPORT_DATA(IReportable reportable)
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

    private static bool TRY_RESOLVE_REPORTABLE(RuntimeObservationTarget target, out IReportable? reportable)
    {
        InstanceManager instances = InstanceManager.Instance;

        reportable = target switch
        {
            RuntimeObservationTarget.INSTANCES => instances,
            RuntimeObservationTarget.LISTENER => CombinedListenerReport.Instance,
            RuntimeObservationTarget.PROTOCOL => CombinedProtocolReport.Instance,
            RuntimeObservationTarget.TASKS => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.Tasks),
            RuntimeObservationTarget.BUFFERS => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.Buffers),
            RuntimeObservationTarget.SESSIONS => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.Sessions),
            RuntimeObservationTarget.CONNECTIONS => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.Connections),
            RuntimeObservationTarget.DISPATCH => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.PacketDispatch),
            RuntimeObservationTarget.OBJECT_POOLS => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.ObjectPools),
            RuntimeObservationTarget.CONNECTION_GUARD => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.ConnectionGuard),
            RuntimeObservationTarget.CONCURRENCY_GATE => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.ConcurrencyGate),
            RuntimeObservationTarget.POLICY_RATE_LIMITER => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.PolicyRateLimiter),
            RuntimeObservationTarget.TOKEN_BUCKET_LIMITER => ReportRegistry.Instance.Get<IReportable>(CoreTelemetryTarget.TokenBucketLimiter),
            RuntimeObservationTarget.NONE or _ => null
        };

        return reportable is not null;
    }

    private static ValueTask<RuntimeObservation> CREATE_RESPONSE(RuntimeObservationTarget target, ProtocolReason reason, BufferLease? bufferLease = null)
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

    #endregion Private Methods

    #region Nested Types

    private sealed class CombinedListenerReport : IReportable
    {
        public static readonly CombinedListenerReport Instance = new();

        private CombinedListenerReport()
        {
        }

        public string GenerateReport() => string.Empty;

        public void WriteReportData(Utf8JsonWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteStartObject();

            IListener? tcp = ReportRegistry.Instance.Get<IListener>(NetworkTransport.TCP);
            if (tcp is not null)
            {
                writer.WritePropertyName("TCP");
                tcp.WriteReportData(writer);
            }

            IListener? udp = ReportRegistry.Instance.Get<IListener>(NetworkTransport.UDP);
            if (udp is not null)
            {
                writer.WritePropertyName("UDP");
                udp.WriteReportData(writer);
            }

            IListener? ws = ReportRegistry.Instance.Get<IListener>(NetworkTransport.WEBSOCKET);
            if (ws is not null)
            {
                writer.WritePropertyName("WEBSOCKET");
                ws.WriteReportData(writer);
            }

            writer.WriteEndObject();
        }
    }

    private sealed class CombinedProtocolReport : IReportable
    {
        public static readonly CombinedProtocolReport Instance = new();

        private CombinedProtocolReport()
        {
        }

        public string GenerateReport() => string.Empty;

        public void WriteReportData(Utf8JsonWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteStartObject();

            IProtocol? tcp = ReportRegistry.Instance.Get<IProtocol>(NetworkTransport.TCP);
            if (tcp is not null)
            {
                writer.WritePropertyName("TCP");
                tcp.WriteReportData(writer);
            }

            IProtocol? udp = ReportRegistry.Instance.Get<IProtocol>(NetworkTransport.UDP);
            if (udp is not null)
            {
                writer.WritePropertyName("UDP");
                udp.WriteReportData(writer);
            }

            IProtocol? ws = ReportRegistry.Instance.Get<IProtocol>(NetworkTransport.WEBSOCKET);
            if (ws is not null)
            {
                writer.WritePropertyName("WEBSOCKET");
                ws.WriteReportData(writer);
            }

            writer.WriteEndObject();
        }
    }

    #endregion Nested Types
}
