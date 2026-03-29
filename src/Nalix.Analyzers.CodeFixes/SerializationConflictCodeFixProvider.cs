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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializationConflictCodeFixProvider)), Shared]
public sealed class SerializationConflictCodeFixProvider : CodeFixProvider
{
    private const string Title = "Remove [SerializeOrder(...)]";
    private const string EquivalenceKey = "Nalix.Serialization.SerializeOrder.Remove";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX015"];

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
                createChangedDocument: cancellationToken => RemoveSerializeOrderAsync(context.Document, member, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> RemoveSerializeOrderAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        foreach (AttributeListSyntax list in member.AttributeLists)
        {
            foreach (AttributeSyntax attribute in list.Attributes)
            {
                if (GetAttributeName(attribute) == "SerializeOrder")
                {
                    editor.RemoveNode(attribute);
                }
            }
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
