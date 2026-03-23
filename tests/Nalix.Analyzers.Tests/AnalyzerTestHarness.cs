using Microsoft.CodeAnalysis.CodeFixes;
using Nalix.Analyzers.CodeFixes;
using System.Threading.Tasks;

namespace Nalix.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    public static Task AssertDiagnosticIdsAsync(string source, params string[] expectedIds)
        => Verifier<ResetForPoolCodeFixProvider>.VerifyAnalyzerAsync(source, expectedIds);

    public static Task AssertCodeFixAsync<TCodeFix>(
        string source,
        string expectedFixedSource,
        string diagnosticId,
        int actionIndex = 0)
        where TCodeFix : CodeFixProvider, new()
        => Verifier<TCodeFix>.VerifyCodeFixAsync(source, expectedFixedSource, diagnosticId, actionIndex);
}
