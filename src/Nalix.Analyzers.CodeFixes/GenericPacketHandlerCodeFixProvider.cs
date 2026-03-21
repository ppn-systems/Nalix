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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GenericPacketHandlerCodeFixProvider)), Shared]
public sealed class GenericPacketHandlerCodeFixProvider : CodeFixProvider
{
    private const string Title = "Remove [PacketOpcode] from generic method";
    private const string EquivalenceKey = "Nalix.PacketOpcode.Generic.RemoveAttribute";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX058"];

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

        AttributeSyntax? packetOpcodeAttribute = method.AttributeLists
            .SelectMany(static x => x.Attributes)
            .FirstOrDefault(a => a.Name.ToString().Contains("PacketOpcode"));

        if (packetOpcodeAttribute is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => RemovePacketOpcodeAttributeAsync(context.Document, method, packetOpcodeAttribute, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> RemovePacketOpcodeAttributeAsync(
        Document document,
        MethodDeclarationSyntax method,
        AttributeSyntax packetOpcodeAttribute,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        AttributeListSyntax? containingList = method.AttributeLists.FirstOrDefault(list => list.Attributes.Contains(packetOpcodeAttribute));
        if (containingList is null)
        {
            return document;
        }

        MethodDeclarationSyntax updatedMethod;
        if (containingList.Attributes.Count == 1)
        {
            updatedMethod = method.RemoveNode(containingList, SyntaxRemoveOptions.KeepNoTrivia)!;
        }
        else
        {
            AttributeListSyntax updatedList = containingList.WithAttributes(
                SyntaxFactory.SeparatedList(containingList.Attributes.Where(a => a != packetOpcodeAttribute)));
            updatedMethod = method.ReplaceNode(containingList, updatedList);
        }

        editor.ReplaceNode(method, updatedMethod);
        return editor.GetChangedDocument();
    }
}
