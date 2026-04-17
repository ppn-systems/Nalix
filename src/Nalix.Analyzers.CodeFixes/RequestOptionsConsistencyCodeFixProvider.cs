// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RequestOptionsConsistencyCodeFixProvider)), Shared]
public sealed class RequestOptionsConsistencyCodeFixProvider : CodeFixProvider
{
    private const string Title = "Set RetryCount to 0";
    private const string EquivalenceKey = "Nalix.RequestOptions.Retry.SetZero";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX057"];

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
        InvocationExpressionSyntax? requestInvocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (requestInvocation is null || requestInvocation.ArgumentList.Arguments.Count < 3)
        {
            return;
        }

        ExpressionSyntax optionsExpression = requestInvocation.ArgumentList.Arguments[2].Expression;
        if (!CanFixOptionsExpression(optionsExpression))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => ApplyFixAsync(context.Document, optionsExpression, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static bool CanFixOptionsExpression(ExpressionSyntax optionsExpression)
    {
        if (optionsExpression is ObjectCreationExpressionSyntax objectCreation)
        {
            return objectCreation.Initializer is not null;
        }

        return optionsExpression
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(IsWithRetryInvocation);
    }

    private static async Task<Document> ApplyFixAsync(Document document, ExpressionSyntax optionsExpression, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        ExpressionSyntax updated = optionsExpression;

        if (optionsExpression is ObjectCreationExpressionSyntax objectCreation
            && objectCreation.Initializer is not null)
        {
            AssignmentExpressionSyntax? retryAssignment = objectCreation.Initializer.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(IsRetryCountAssignment);

            if (retryAssignment is not null)
            {
                updated = optionsExpression.ReplaceNode(
                    retryAssignment.Right,
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));
            }
            else
            {
                InitializerExpressionSyntax initializer = objectCreation.Initializer;
                List<ExpressionSyntax> expressions = new(initializer.Expressions)
                {
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName("RetryCount"),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)))
                };

                updated = objectCreation.WithInitializer(
                    initializer.WithExpressions(SyntaxFactory.SeparatedList(expressions)));
            }
        }
        else
        {
            InvocationExpressionSyntax? withRetryCall = optionsExpression
                .DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(IsWithRetryInvocation);

            if (withRetryCall is not null)
            {
                ArgumentSyntax retryArgument = withRetryCall.ArgumentList.Arguments[0];
                updated = optionsExpression.ReplaceNode(
                    retryArgument.Expression,
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));
            }
        }

        editor.ReplaceNode(optionsExpression, updated.WithTriviaFrom(optionsExpression));
        return editor.GetChangedDocument();
    }

    private static bool IsWithRetryInvocation(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
               && memberAccess.Name.Identifier.ValueText == "WithRetry"
               && invocation.ArgumentList.Arguments.Count == 1;
    }

    private static bool IsRetryCountAssignment(AssignmentExpressionSyntax assignment)
    {
        return assignment.Left switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText == "RetryCount",
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == "RetryCount",
            _ => false
        };
    }
}

