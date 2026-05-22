using Nalix.Observability.Contracts;

namespace Nalix.Dashboard.Application.Abstractions;

internal interface IAdminClient
{
    Task SetApiKeyAsync(string apiKey);

    Task RefreshAsync(RuntimeObservationTarget target, CancellationToken ct);

    Task RefreshAllAsync(CancellationToken ct);

    Task PingAsync(CancellationToken ct);
}
