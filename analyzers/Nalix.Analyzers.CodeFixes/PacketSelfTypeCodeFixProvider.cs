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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PacketSelfTypeCodeFixProvider)), Shared]
public sealed class PacketSelfTypeCodeFixProvider : CodeFixProvider
{
    private const string PacketBaseTitle = "Fix PacketBase<TSelf> to use containing type";
    private const string PacketBaseEquivalenceKey = "Nalix.PacketBase.SelfType.Fix";
    private const string PacketDeserializerTitle = "Fix IPacketDeserializer<T> to use containing type";
    private const string PacketDeserializerEquivalenceKey = "Nalix.PacketDeserializer.SelfType.Fix";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX010", "NALIX011"];

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

        string typeName = typeDeclaration.Identifier.ValueText;

        if (diagnostic.Id == "NALIX010" && typeDeclaration.BaseList is not null)
        {
            foreach (BaseTypeSyntax baseType in typeDeclaration.BaseList.Types)
            {
                if (baseType.Type is GenericNameSyntax genericName && genericName.Identifier.ValueText == "PacketBase")
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: PacketBaseTitle,
                            createChangedDocument: cancellationToken => ReplaceGenericTypeArgumentAsync(context.Document, genericName, typeName, cancellationToken),
                            equivalenceKey: PacketBaseEquivalenceKey),
                        diagnostic);
                    return;
                }
            }
        }

        if (diagnostic.Id == "NALIX011" && typeDeclaration.BaseList is not null)
        {
            foreach (BaseTypeSyntax baseType in typeDeclaration.BaseList.Types)
            {
                if (baseType.Type is GenericNameSyntax genericName && genericName.Identifier.ValueText == "IPacketDeserializer")
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: PacketDeserializerTitle,
                            createChangedDocument: cancellationToken => ReplaceGenericTypeArgumentAsync(context.Document, genericName, typeName, cancellationToken),
                            equivalenceKey: PacketDeserializerEquivalenceKey),
                        diagnostic);
                    return;
                }
            }
        }
    }

    private static async Task<Document> ReplaceGenericTypeArgumentAsync(Document document, GenericNameSyntax genericName, string typeName, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        GenericNameSyntax updatedGeneric = genericName.WithTypeArgumentList(
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                    SyntaxFactory.IdentifierName(typeName))));

        editor.ReplaceNode(genericName, updatedGeneric);
        return editor.GetChangedDocument();
    }
}
