using Nalix.Abstractions.Networking.Protocols;
using Nalix.Examples.Contracts.Packets;

namespace Nalix.Examples.Dashboard.Services;

internal sealed record DashboardReportSnapshot(
    GenerationReportTarget Target,
    ProtocolReason Reason,
    IReadOnlyDictionary<string, object?> Data,
    DateTimeOffset ReceivedAt);
