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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializeOrderMissingCodeFixProvider)), Shared]
public sealed class SerializeOrderMissingCodeFixProvider : CodeFixProvider
{
    private const string AddIgnoreTitle = "Add [SerializeIgnore]";
    private const string AddIgnoreEquivalenceKey = "Nalix.Serialization.SerializeIgnore.Add";
    private const string AddOrderTitle = "Add [SerializeOrder(next)]";
    private const string AddOrderEquivalenceKey = "Nalix.Serialization.SerializeOrder.AddNext";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX013"];

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
        MemberDeclarationSyntax? member = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        member ??= node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (member is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: AddIgnoreTitle,
                createChangedDocument: cancellationToken => AddSerializeIgnoreAsync(context.Document, member, cancellationToken),
                equivalenceKey: AddIgnoreEquivalenceKey),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: AddOrderTitle,
                createChangedDocument: cancellationToken => AddSerializeOrderAsync(context.Document, member, cancellationToken),
                equivalenceKey: AddOrderEquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> AddSerializeIgnoreAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("SerializeIgnore"))));

        editor.ReplaceNode(member, AddAttributeList(member, attributeList));
        return editor.GetChangedDocument();
    }

    private static async Task<Document> AddSerializeOrderAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return document;
        }

        TypeDeclarationSyntax? containingType = member.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is null)
        {
            return document;
        }

        HashSet<int> usedOrders = [];
        foreach (MemberDeclarationSyntax sibling in containingType.Members)
        {
            foreach (AttributeSyntax attribute in sibling.AttributeLists.SelectMany(static list => list.Attributes))
            {
                if (GetAttributeName(attribute) != "SerializeOrder" || attribute.ArgumentList?.Arguments.Count != 1)
                {
                    continue;
                }

                Optional<object?> constantValue = semanticModel.GetConstantValue(attribute.ArgumentList.Arguments[0].Expression, cancellationToken);
                switch (constantValue.Value)
                {
                    case int intValue:
                        _ = usedOrders.Add(intValue);
                        break;
                    case byte byteValue:
                        _ = usedOrders.Add(byteValue);
                        break;
                    case short shortValue:
                        _ = usedOrders.Add(shortValue);
                        break;
                    default:
                        break;
                }
            }
        }

        int nextOrder = 0;
        while (usedOrders.Contains(nextOrder))
        {
            nextOrder++;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("SerializeOrder"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        SyntaxFactory.Literal(nextOrder))))))));

        editor.ReplaceNode(member, AddAttributeList(member, attributeList));
        return editor.GetChangedDocument();
    }

    private static MemberDeclarationSyntax AddAttributeList(MemberDeclarationSyntax member, AttributeListSyntax attributeList)
        => member switch
        {
            PropertyDeclarationSyntax property => property.AddAttributeLists(attributeList),
            FieldDeclarationSyntax field => field.AddAttributeLists(attributeList),
            _ => member
        };

    private static string GetAttributeName(AttributeSyntax attribute)
    {
        return attribute.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => attribute.Name.ToString()
        };
    }
}
