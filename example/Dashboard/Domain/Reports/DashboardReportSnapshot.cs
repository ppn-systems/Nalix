using Nalix.Abstractions.Networking.Protocols;
using Contracts;

namespace Dashboard.Domain.Reports;

public sealed record DashboardReportSnapshot(
    GenerationReportTarget Target,
    ProtocolReason Reason,
    IReadOnlyDictionary<string, object?> Data,
    DateTimeOffset ReceivedAt);
