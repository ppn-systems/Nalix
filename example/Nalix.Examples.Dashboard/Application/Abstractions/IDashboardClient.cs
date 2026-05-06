using Nalix.Examples.Contracts;

namespace Nalix.Examples.Dashboard.Application.Abstractions;

internal interface IDashboardClient
{
    Task SetApiKeyAsync(string apiKey);

    Task RefreshAsync(GenerationReportTarget target, CancellationToken ct);

    Task RefreshAllAsync(CancellationToken ct);

    Task PingAsync(CancellationToken ct);
}
