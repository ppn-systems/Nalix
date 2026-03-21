// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Nalix.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantPacketCastCodeFixProvider)), Shared]
public sealed class RedundantPacketCastCodeFixProvider : CodeFixProvider
{
    private const string Title = "Remove redundant cast";
    private const string EquivalenceKey = "Nalix.PacketContext.Cast.Remove";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX055"];

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
        CastExpressionSyntax? cast = node.FirstAncestorOrSelf<CastExpressionSyntax>();
        if (cast is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => RemoveCastAsync(context.Document, cast, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> RemoveCastAsync(Document document, CastExpressionSyntax cast, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.ReplaceNode(cast, cast.Expression.WithTriviaFrom(cast));
        return editor.GetChangedDocument();
    }
}

