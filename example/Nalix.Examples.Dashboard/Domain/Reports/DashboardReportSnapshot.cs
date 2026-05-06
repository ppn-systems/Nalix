using Nalix.Abstractions.Networking.Protocols;
using Nalix.Examples.Contracts;

namespace Nalix.Examples.Dashboard.Domain.Reports;

public sealed record DashboardReportSnapshot(
    GenerationReportTarget Target,
    ProtocolReason Reason,
    IReadOnlyDictionary<string, object?> Data,
    DateTimeOffset ReceivedAt);
