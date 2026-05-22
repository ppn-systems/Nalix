using Nalix.Observability.Contracts;

namespace Nalix.Dashboard.Application.Abstractions;

internal interface IReportParser
{
    RuntimeObservationTarget Target { get; }

    bool CanParse(RuntimeObservationTarget target);

    object? Parse(string ObservationData);
}

internal interface IReportParser<TReport> : IReportParser where TReport : class
{
    TReport? ParseTyped(string ObservationData);
}
