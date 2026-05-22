using Contracts;

namespace Nalix.Dashboard.Application.Abstractions;

internal interface IAdminClient
{
    Task SetApiKeyAsync(string apiKey);

    Task RefreshAsync(GenerationReportTarget target, CancellationToken ct);

    Task RefreshAllAsync(CancellationToken ct);

    Task PingAsync(CancellationToken ct);
}
