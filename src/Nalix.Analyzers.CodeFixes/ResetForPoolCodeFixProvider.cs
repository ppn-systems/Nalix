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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ResetForPoolCodeFixProvider)), Shared]
public sealed class ResetForPoolCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add base.ResetForPool()";
    private const string EquivalenceKey = "Nalix.Packet.ResetForPool.AddBaseCall";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX020"];

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
        MethodDeclarationSyntax? method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => AddBaseResetForPoolAsync(context.Document, method, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> AddBaseResetForPoolAsync(Document document, MethodDeclarationSyntax method, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        StatementSyntax baseCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.BaseExpression(),
                    SyntaxFactory.IdentifierName("ResetForPool"))));

        MethodDeclarationSyntax updatedMethod;
        if (method.Body is not null)
        {
            updatedMethod = method.WithBody(method.Body.WithStatements(method.Body.Statements.Insert(0, baseCall)));
        }
        else if (method.ExpressionBody is not null)
        {
            updatedMethod = method
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(
                    SyntaxFactory.Block(
                        baseCall,
                        SyntaxFactory.ExpressionStatement(method.ExpressionBody.Expression)));
        }
        else
        {
            return document;
        }

        editor.ReplaceNode(method, updatedMethod);
        return editor.GetChangedDocument();
    }
}
