// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Nalix.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DispatchLoopCountCodeFixProvider)), Shared]
public sealed class DispatchLoopCountCodeFixProvider : CodeFixProvider
{
    private const string Title = "Clamp dispatch loop count to supported range";
    private const string EquivalenceKey = "Nalix.DispatchLoopCount.Clamp";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX047"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        InvocationExpressionSyntax? invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null || invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        ExpressionSyntax currentExpression = invocation.ArgumentList.Arguments[0].Expression;
        Optional<object?> constant = semanticModel.GetConstantValue(currentExpression, context.CancellationToken);
        if (!constant.HasValue)
        {
            return;
        }

        int? currentValue = constant.Value switch
        {
            int i => i,
            short s => s,
            byte b => b,
            sbyte sb => sb,
            _ => null
        };

        if (!currentValue.HasValue)
        {
            return;
        }

        int clamped = currentValue.Value < 1 ? 1 : 64;
        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => ApplyClampAsync(context.Document, currentExpression, clamped, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> ApplyClampAsync(Document document, ExpressionSyntax originalExpression, int clampedValue, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        ExpressionSyntax replacement = SyntaxFactory.LiteralExpression(
            SyntaxKind.NumericLiteralExpression,
            SyntaxFactory.Literal(clampedValue));

        editor.ReplaceNode(originalExpression, replacement.WithTriviaFrom(originalExpression));
        return editor.GetChangedDocument();
    }
}

