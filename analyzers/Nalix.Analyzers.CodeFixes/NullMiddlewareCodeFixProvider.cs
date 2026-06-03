// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Nalix.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullMiddlewareCodeFixProvider)), Shared]
public sealed class NullMiddlewareCodeFixProvider : CodeFixProvider
{
    private const string RemoveRegistrationTitle = "Remove null middleware registration";
    private const string RemoveRegistrationEquivalenceKey = "Nalix.Middleware.Null.RemoveStatement";
    private const string ThrowExpressionTitle = "Replace null with throw expression";
    private const string ThrowExpressionEquivalenceKey = "Nalix.Middleware.Null.ThrowExpression";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX056"];

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
        if (invocation is null || invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        ArgumentSyntax? nullArgument = invocation.ArgumentList.Arguments.FirstOrDefault(
            argument => argument.Expression.IsKind(SyntaxKind.NullLiteralExpression));

        if (nullArgument is null)
        {
            return;
        }

        if (invocation.Parent is ExpressionStatementSyntax expressionStatement)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: RemoveRegistrationTitle,
                    createChangedDocument: cancellationToken => RemoveStatementAsync(context.Document, expressionStatement, cancellationToken),
                    equivalenceKey: RemoveRegistrationEquivalenceKey),
                diagnostic);
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: ThrowExpressionTitle,
                createChangedDocument: cancellationToken => ReplaceWithThrowExpressionAsync(context.Document, nullArgument, cancellationToken),
                equivalenceKey: ThrowExpressionEquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> RemoveStatementAsync(Document document, ExpressionStatementSyntax statement, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.RemoveNode(statement);
        return editor.GetChangedDocument();
    }

    private static async Task<Document> ReplaceWithThrowExpressionAsync(Document document, ArgumentSyntax nullArgument, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        ThrowExpressionSyntax throwExpression = SyntaxFactory.ThrowExpression(
            SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.AliasQualifiedName(
                            SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)),
                            SyntaxFactory.IdentifierName("System")),
                        SyntaxFactory.IdentifierName("ArgumentNullException")))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal("middleware")))))));

        editor.ReplaceNode(nullArgument.Expression, throwExpression.WithTriviaFrom(nullArgument.Expression));
        return editor.GetChangedDocument();
    }
}

