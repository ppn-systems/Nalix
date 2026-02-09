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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MiddlewareCodeFixProvider)), Shared]
public sealed class MiddlewareCodeFixProvider : CodeFixProvider
{
    private const string AddOrderTitle = "Add [MiddlewareOrder(0)]";
    private const string AddOrderEquivalenceKey = "Nalix.Middleware.Order.AddDefault";
    private const string RemoveAlwaysExecuteTitle = "Remove AlwaysExecute = true";
    private const string RemoveAlwaysExecuteEquivalenceKey = "Nalix.Middleware.Stage.RemoveAlwaysExecute";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX030", "NALIX031", "NALIX032"];

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
        TypeDeclarationSyntax? typeDeclaration = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDeclaration is null)
        {
            return;
        }

        if (diagnostic.Id is "NALIX030" or "NALIX031")
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: AddOrderTitle,
                    createChangedDocument: cancellationToken => AddMiddlewareOrderAsync(context.Document, typeDeclaration, cancellationToken),
                    equivalenceKey: AddOrderEquivalenceKey),
                diagnostic);
        }

        if (diagnostic.Id == "NALIX032")
        {
            AttributeSyntax? stageAttribute = typeDeclaration.AttributeLists
                .SelectMany(static list => list.Attributes)
                .FirstOrDefault(attribute => attribute.Name.ToString().Contains("MiddlewareStage"));
            if (stageAttribute is null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: RemoveAlwaysExecuteTitle,
                    createChangedDocument: cancellationToken => RemoveAlwaysExecuteAsync(context.Document, stageAttribute, cancellationToken),
                    equivalenceKey: RemoveAlwaysExecuteEquivalenceKey),
                diagnostic);
        }
    }

    private static async Task<Document> AddMiddlewareOrderAsync(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("MiddlewareOrder"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        SyntaxFactory.Literal(0))))))));

        editor.ReplaceNode(typeDeclaration, typeDeclaration.AddAttributeLists(attributeList));
        return editor.GetChangedDocument();
    }

    private static async Task<Document> RemoveAlwaysExecuteAsync(Document document, AttributeSyntax stageAttribute, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        AttributeArgumentListSyntax? argumentList = stageAttribute.ArgumentList;
        if (argumentList is null)
        {
            return document;
        }

        SeparatedSyntaxList<AttributeArgumentSyntax> keptArguments = SyntaxFactory.SeparatedList(
            argumentList.Arguments.Where(static argument =>
                argument.NameEquals?.Name.Identifier.ValueText != "AlwaysExecute"));

        AttributeSyntax updatedAttribute = stageAttribute.WithArgumentList(
            keptArguments.Count == 0 ? null : argumentList.WithArguments(keptArguments));

        editor.ReplaceNode(stageAttribute, updatedAttribute);
        return editor.GetChangedDocument();
    }
}
