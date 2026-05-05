// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Examples.Contracts.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Tasks;
using Nalix.Network.RateLimiting;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Pooling;

namespace Nalix.Examples.Backend.Handlers;

[PacketController("ExampleGenerationReport")]
public sealed class GenerationReportHandlers
{
    private const int MaxDepth = 4;
    private const int MaxItemsPerCollection = 24;
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

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

        IDictionary<string, object> raw = reportable!.GetReportData();
        string dataJson = NormalizeReportData(raw);

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

    private static string NormalizeReportData(IDictionary<string, object> raw)
    {
        Dictionary<string, object?> data = new(raw.Count, StringComparer.Ordinal);

        foreach (KeyValuePair<string, object> row in raw)
        {
            data[row.Key] = NormalizeJsonValue(row.Value, depth: 0);
        }

        return JsonSerializer.Serialize(data, s_jsonOptions);
    }

    private static object? NormalizeJsonValue(object? value, int depth)
    {
        if (value is null)
        {
            return null;
        }

        if (depth >= MaxDepth)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal)
        {
            return value;
        }

        if (value is double doubleValue)
        {
            return double.IsFinite(doubleValue)
                ? doubleValue
                : doubleValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value is float floatValue)
        {
            return float.IsFinite(floatValue)
                ? floatValue
                : floatValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value is DateTime or DateTimeOffset)
        {
            return value;
        }

        if (value is DateOnly or TimeOnly or TimeSpan)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        if (value is Enum)
        {
            return value.ToString();
        }

        if (value is System.Collections.IDictionary dictionary)
        {
            return NormalizeDictionary(dictionary, depth);
        }

        if (value is System.Collections.IEnumerable sequence)
        {
            return NormalizeSequence(sequence, depth);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static Dictionary<string, object?> NormalizeDictionary(System.Collections.IDictionary dictionary, int depth)
    {
        Dictionary<string, object?> data = new(dictionary.Count, StringComparer.Ordinal);
        int count = 0;

        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            if (count >= MaxItemsPerCollection)
            {
                break;
            }

            string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
            data[key] = NormalizeJsonValue(entry.Value, depth + 1);
            count++;
        }

        return data;
    }

    private static List<object?> NormalizeSequence(System.Collections.IEnumerable sequence, int depth)
    {
        List<object?> data = [];
        int count = 0;

        foreach (object? item in sequence)
        {
            if (count >= MaxItemsPerCollection)
            {
                break;
            }

            data.Add(NormalizeJsonValue(item, depth + 1));
            count++;
        }

        return data;
    }
}
