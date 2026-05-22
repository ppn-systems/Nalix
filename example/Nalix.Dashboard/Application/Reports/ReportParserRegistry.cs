using Nalix.Observability.Contracts;
using Nalix.Dashboard.Application.Abstractions;

namespace Nalix.Dashboard.Application.Reports;

internal sealed class ReportParserRegistry
{
    private readonly IReadOnlyDictionary<RuntimeObservationTarget, IReportParser> _parsers;

    public ReportParserRegistry(IEnumerable<IReportParser> parsers)
    {
        Dictionary<RuntimeObservationTarget, IReportParser> map = [];
        foreach (IReportParser parser in parsers)
        {
            map[parser.Target] = parser;
        }

        _parsers = map;
    }

    public IReportParser? Get(RuntimeObservationTarget target)
        => _parsers.TryGetValue(target, out IReportParser? parser) ? parser : null;

    public TReport? Parse<TReport>(RuntimeObservationTarget target, string ObservationData) where TReport : class
    {
        if (!_parsers.TryGetValue(target, out IReportParser? parser) || parser is not IReportParser<TReport> typed)
        {
            return null;
        }

        return typed.ParseTyped(ObservationData);
    }
}
