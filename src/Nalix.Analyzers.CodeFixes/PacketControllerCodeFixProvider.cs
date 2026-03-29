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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PacketControllerCodeFixProvider)), Shared]
public sealed class PacketControllerCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add [PacketController]";
    private const string EquivalenceKey = "Nalix.PacketController.Add";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX008"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics[0];
        string? controllerName = diagnostic.GetMessage()
            .Split('\'')
            .Skip(1)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(controllerName))
        {
            return;
        }

        TypeDeclarationSyntax? targetType = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(type =>
            {
                INamedTypeSymbol? symbol = semanticModel.GetDeclaredSymbol(type, context.CancellationToken);
                return symbol?.Name == controllerName;
            });

        if (targetType is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => AddPacketControllerAsync(context.Document, targetType, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> AddPacketControllerAsync(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("PacketController"))));

        editor.ReplaceNode(typeDeclaration, typeDeclaration.AddAttributeLists(attributeList));
        return editor.GetChangedDocument();
    }
}
