// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Nalix.Analyzers.Analyzers;
using Xunit;

namespace Nalix.Analyzers.Tests;

internal static class Verifier<TCodeFix>
    where TCodeFix : CodeFixProvider, new()
{
    public static async Task VerifyAnalyzerAsync(string source, params string[] expectedDiagnosticIds)
    {
        Document document = CreateDocument(TestSources.Prelude + source);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document).ConfigureAwait(false);
        string[] actual = [.. diagnostics.Select(d => d.Id).OrderBy(x => x)];
        string[] expected = [.. expectedDiagnosticIds.OrderBy(x => x)];
        Assert.Equal(expected, actual);
    }

    public static async Task VerifyCodeFixAsync(string source, string fixedSource, string diagnosticId)
        => await VerifyCodeFixAsync(source, fixedSource, diagnosticId, 0).ConfigureAwait(false);

    public static async Task VerifyCodeFixAsync(string source, string fixedSource, string diagnosticId, int actionIndex)
        => await VerifyCodeFixAsync(source, fixedSource, diagnosticId, actionIndex, expectedTitle: null, expectedEquivalenceKey: null).ConfigureAwait(false);

    public static async Task VerifyCodeFixAsync(
        string source,
        string fixedSource,
        string diagnosticId,
        int actionIndex,
        string? expectedTitle,
        string? expectedEquivalenceKey)
    {
        Document document = CreateDocument(TestSources.Prelude + source);
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(document).ConfigureAwait(false);
        Diagnostic diagnostic = diagnostics.First(d => d.Id == diagnosticId);
        await VerifyCodeFixCoreAsync(document, diagnostic, fixedSource, actionIndex, expectedTitle, expectedEquivalenceKey).ConfigureAwait(false);
    }

    public static async Task VerifyCodeFixWithSyntheticDiagnosticAsync(
        string source,
        string fixedSource,
        string diagnosticId,
        string diagnosticMessage,
        string locateText,
        int actionIndex = 0)
        => await VerifyCodeFixWithSyntheticDiagnosticAsync(
            source,
            fixedSource,
            diagnosticId,
            diagnosticMessage,
            locateText,
            actionIndex,
            expectedTitle: null,
            expectedEquivalenceKey: null).ConfigureAwait(false);

    public static async Task VerifyCodeFixWithSyntheticDiagnosticAsync(
        string source,
        string fixedSource,
        string diagnosticId,
        string diagnosticMessage,
        string locateText,
        int actionIndex,
        string? expectedTitle,
        string? expectedEquivalenceKey)
    {
        Document document = CreateDocument(TestSources.Prelude + source);
        string fullSource = TestSources.Prelude + source;
        int start = fullSource.IndexOf(locateText, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find text '{locateText}' in source.");
        SyntaxTree syntaxTree = (await document.GetSyntaxTreeAsync().ConfigureAwait(false))!;
        FileLinePositionSpan lineSpan = syntaxTree.GetLineSpan(new TextSpan(start, locateText.Length));

        DiagnosticDescriptor descriptor = new(
            diagnosticId,
            diagnosticId,
            "{0}",
            "Test",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        Diagnostic diagnostic = Diagnostic.Create(
            descriptor,
            Location.Create(
                document.FilePath ?? "Test.cs",
                new TextSpan(start, locateText.Length),
                lineSpan.Span),
            diagnosticMessage);
        await VerifyCodeFixCoreAsync(document, diagnostic, fixedSource, actionIndex, expectedTitle, expectedEquivalenceKey).ConfigureAwait(false);
    }

    private static async Task VerifyCodeFixCoreAsync(
        Document document,
        Diagnostic diagnostic,
        string fixedSource,
        int actionIndex,
        string? expectedTitle,
        string? expectedEquivalenceKey)
    {
        CodeFixProvider codeFix = new TCodeFix();
        List<CodeAction> actions = [];
        CodeFixContext context = new(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await codeFix.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        Assert.True(actions.Count > actionIndex, $"Expected at least {actionIndex + 1} code fix action(s), got {actions.Count}.");

        CodeAction action = actions[actionIndex];
        if (expectedTitle is not null)
        {
            Assert.Equal(expectedTitle, action.Title);
        }

        if (expectedEquivalenceKey is not null)
        {
            Assert.Equal(expectedEquivalenceKey, action.EquivalenceKey);
        }

        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        ApplyChangesOperation applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        Document updatedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
        string actual = (await updatedDocument.GetTextAsync().ConfigureAwait(false)).ToString();
        Assert.Equal(Normalize(TestSources.Prelude + fixedSource), Normalize(actual));
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document)
    {
        Compilation compilation = await document.Project.GetCompilationAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Compilation could not be created.");

        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers(
            [new NalixUsageAnalyzer()]);

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static Document CreateDocument(string source)
    {
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = new AdhocWorkspace().CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Task).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(ValueTuple<>).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location))
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();
}
