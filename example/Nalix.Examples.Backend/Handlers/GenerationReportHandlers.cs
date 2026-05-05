// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using System.Diagnostics.CodeAnalysis;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Tasks;
using Nalix.Examples.Packets;
using Nalix.Runtime.Dispatching;

namespace Nalix.Examples.Backend.Handlers;

[PacketController("ExampleGenerationReport")]
public sealed class GenerationReportHandlers
{
    [PacketEncryption(true)]
    [PacketPermission(PermissionLevel.SYSTEM_ADMINISTRATOR)]
    [PacketOpcode(GenerationReport.OpCodeValue)]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned packet is sent and disposed by the Nalix return handler.")]
    public static ValueTask<GenerationReport> HandleAsync(IPacketContext<GenerationReport> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GenerationReport request = context.Packet;
        GenerationReport response = GenerationReport.Create();

        if (request.Stage != GenerationReportStage.REQUEST || !request.Validate(out _))
        {
            response.Initialize(GenerationReportStage.RESPONSE, request.Target, ProtocolReason.MALFORMED_PACKET);
            return ValueTask.FromResult(response);
        }

        if (!TryResolveReportable(request.Target, out IReportable? reportable))
        {
            response.Initialize(GenerationReportStage.RESPONSE, request.Target, ProtocolReason.NOT_FOUND);
            return ValueTask.FromResult(response);
        }

        IDictionary<string, object> raw = reportable!.GetReportData();
        Dictionary<string, object> data = raw as Dictionary<string, object>
            ?? new Dictionary<string, object>(raw, StringComparer.Ordinal);

        response.Initialize(
            GenerationReportStage.RESPONSE,
            request.Target,
            ProtocolReason.NONE,
            data);

        return ValueTask.FromResult(response);
    }

    private static bool TryResolveReportable(GenerationReportTarget target, out IReportable? reportable)
    {
        InstanceManager instances = InstanceManager.Instance;

        reportable = target switch
        {
            GenerationReportTarget.DISPATCH => instances.GetExistingInstance<IPacketDispatch>(),
            GenerationReportTarget.TASKS => instances.GetExistingInstance<TaskManager>(),
            GenerationReportTarget.BUFFERS => instances.GetExistingInstance<BufferPoolManager>(),
            GenerationReportTarget.CONNECTIONS => instances.GetExistingInstance<IConnectionHub>(),
            GenerationReportTarget.INSTANCES => instances,
            _ => null
        };

        return reportable is not null;
    }
}
