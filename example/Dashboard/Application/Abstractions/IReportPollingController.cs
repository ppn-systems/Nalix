using Contracts;

namespace Nalix.Dashboard.Application.Abstractions;

internal interface IReportPollingController
{
    GenerationReportTarget? ActiveTarget { get; }

    string? ActiveRoute { get; }

    bool IsPolling { get; }

    bool IsPaused { get; }

    int CurrentIntervalMs { get; }

    DateTimeOffset? LastRequestUtc { get; }

    DateTimeOffset? LastSuccessUtc { get; }

    DateTimeOffset? LastFailureUtc { get; }

    string? LastFailureMessage { get; }

    event Action? Changed;

    void Activate(GenerationReportTarget target, string route, int intervalMs);

    void Deactivate(string route);

    void Pause();

    void Resume();

    void SetInterval(int intervalMs);

    Task RequestOnceAsync(GenerationReportTarget target, CancellationToken ct);
}
