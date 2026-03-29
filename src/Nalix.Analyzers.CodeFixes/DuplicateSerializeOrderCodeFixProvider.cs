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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DuplicateSerializeOrderCodeFixProvider)), Shared]
public sealed class DuplicateSerializeOrderCodeFixProvider : CodeFixProvider
{
    private const string Title = "Assign next available SerializeOrder";
    private const string EquivalenceKey = "Nalix.Serialization.SerializeOrder.ResolveDuplicate";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX014"];

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
                title: Title,
                createChangedDocument: cancellationToken => FixDuplicateSerializeOrderAsync(context.Document, member, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> FixDuplicateSerializeOrderAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
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
        foreach (AttributeSyntax attribute in member.AttributeLists.SelectMany(static list => list.Attributes))
        {
            if (GetAttributeName(attribute) != "SerializeOrder")
            {
                continue;
            }

            AttributeSyntax updatedAttribute = attribute.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                SyntaxFactory.Literal(nextOrder))))));

            editor.ReplaceNode(attribute, updatedAttribute);
            break;
        }

        return editor.GetChangedDocument();
    }

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
