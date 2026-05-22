using Contracts;
using Nalix.Dashboard.Application.Abstractions;

namespace Nalix.Dashboard.Application.Reports;

internal sealed class ReportParserRegistry
{
    private readonly IReadOnlyDictionary<GenerationReportTarget, IReportParser> _parsers;

    public ReportParserRegistry(IEnumerable<IReportParser> parsers)
    {
        Dictionary<GenerationReportTarget, IReportParser> map = [];
        foreach (IReportParser parser in parsers)
        {
            map[parser.Target] = parser;
        }

        _parsers = map;
    }

    public IReportParser? Get(GenerationReportTarget target)
        => _parsers.TryGetValue(target, out IReportParser? parser) ? parser : null;

    public TReport? Parse<TReport>(GenerationReportTarget target, string dataJson) where TReport : class
    {
        if (!_parsers.TryGetValue(target, out IReportParser? parser) || parser is not IReportParser<TReport> typed)
        {
            return null;
        }

        return typed.ParseTyped(dataJson);
    }
}
