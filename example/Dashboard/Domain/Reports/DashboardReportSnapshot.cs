using Contracts;
using Nalix.Abstractions.Networking.Protocols;

namespace Nalix.Dashboard.Domain.Reports;

public sealed record DashboardReportSnapshot(
    GenerationReportTarget Target,
    ProtocolReason Reason,
    string DataJson,
    IReadOnlyDictionary<string, object?> Data,
    DateTimeOffset ReceivedAt);
