// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Examples.Contracts.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Tasks;
using Nalix.Runtime.Dispatching;

namespace Nalix.Examples.Backend.Handlers;

[PacketController("ExampleGenerationReport")]
public sealed class GenerationReportHandlers
{
    private const int MaxDepth = 4;
    private const int MaxItemsPerCollection = 24;
    private const int MaxValueLength = 2048;

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
        Dictionary<string, string> data = NormalizeReportData(raw);

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
            GenerationReportTarget.NONE => throw new NotImplementedException(),
            _ => null
        };

        return reportable is not null;
    }

    private static Dictionary<string, string> NormalizeReportData(IDictionary<string, object> raw)
    {
        Dictionary<string, string> data = new(raw.Count, StringComparer.Ordinal);

        foreach (KeyValuePair<string, object> row in raw)
        {
            data[row.Key] = Limit(FormatReportValue(row.Value, depth: 0));
        }

        return data;
    }

    private static string FormatReportValue(object? value, int depth)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (depth >= MaxDepth)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        if (value is System.Collections.IDictionary dictionary)
        {
            return FormatDictionary(dictionary, depth);
        }

        if (value is System.Collections.IEnumerable sequence)
        {
            return FormatSequence(sequence, depth);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatDictionary(System.Collections.IDictionary dictionary, int depth)
    {
        StringBuilder builder = new();
        int count = 0;

        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            if (count >= MaxItemsPerCollection)
            {
                _ = builder.Append(", ...");
                break;
            }

            if (count > 0)
            {
                _ = builder.Append(", ");
            }

            _ = builder.Append(Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
            _ = builder.Append(": ");
            _ = builder.Append(FormatReportValue(entry.Value, depth + 1));
            count++;
        }

        return builder.ToString();
    }

    private static string FormatSequence(System.Collections.IEnumerable sequence, int depth)
    {
        StringBuilder builder = new();
        int count = 0;

        foreach (object? item in sequence)
        {
            if (count >= MaxItemsPerCollection)
            {
                _ = builder.Append(", ...");
                break;
            }

            if (count > 0)
            {
                _ = builder.Append(", ");
            }

            _ = builder.Append(FormatReportValue(item, depth + 1));
            count++;
        }

        return builder.ToString();
    }

    private static string Limit(string value)
        => value.Length <= MaxValueLength
            ? value
            : string.Concat(value.AsSpan(0, MaxValueLength), "...");
}
