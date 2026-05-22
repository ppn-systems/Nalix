using Nalix.Observability.Contracts;
using Nalix.Abstractions.Networking.Protocols;

namespace Nalix.Dashboard.Domain.Reports;

public sealed record DashboardReportSnapshot(
    RuntimeObservationTarget Target,
    ProtocolReason Reason,
    string DataJson,
    IReadOnlyDictionary<string, object?> Data,
    DateTimeOffset ReceivedAt);
