using Contracts;

namespace Nalix.Dashboard.Application.Abstractions;

internal interface IReportParser
{
    GenerationReportTarget Target { get; }

    bool CanParse(GenerationReportTarget target);

    object? Parse(string dataJson);
}

internal interface IReportParser<TReport> : IReportParser where TReport : class
{
    TReport? ParseTyped(string dataJson);
}
