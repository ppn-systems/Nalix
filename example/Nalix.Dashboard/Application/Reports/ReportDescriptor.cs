using Nalix.Observability.Contracts;
using Nalix.Abstractions.Security;

namespace Nalix.Dashboard.Application.Reports;

public sealed record ReportDescriptor(
    RuntimeObservationTarget Target,
    string Route,
    string Title,
    string Description,
    int DefaultPollingIntervalMs,
    int MinimumPollingIntervalMs,
    int MaximumPollingIntervalMs,
    bool SupportsRawJsonPreview,
    bool SupportsCharts,
    PermissionLevel RequiredPermissionLevel = PermissionLevel.SYSTEM_ADMINISTRATOR);
