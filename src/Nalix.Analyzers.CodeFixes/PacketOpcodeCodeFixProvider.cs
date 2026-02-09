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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PacketOpcodeCodeFixProvider)), Shared]
public sealed class PacketOpcodeCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add [PacketOpcode(...)]";
    private const string EquivalenceKey = "Nalix.PacketOpcode.Add";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX002"];

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
                createChangedDocument: cancellationToken => AddPacketOpcodeAsync(context.Document, method, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> AddPacketOpcodeAsync(Document document, MethodDeclarationSyntax method, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new System.InvalidOperationException("Semantic model is required to add PacketOpcode.");

        ushort suggestedOpcode = GetSuggestedOpcode(method, semanticModel, cancellationToken);
        string opcodeText = $"0x{suggestedOpcode:X4}";

        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("PacketOpcode"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        SyntaxFactory.Literal(opcodeText, suggestedOpcode))))))));

        editor.ReplaceNode(method, method.AddAttributeLists(attributeList));
        return editor.GetChangedDocument();
    }

    private static ushort GetSuggestedOpcode(MethodDeclarationSyntax method, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (method.Parent is not TypeDeclarationSyntax containingType)
        {
            return 0;
        }

        bool[] used = new bool[ushort.MaxValue + 1];

        foreach (MethodDeclarationSyntax siblingMethod in containingType.Members.OfType<MethodDeclarationSyntax>())
        {
            foreach (AttributeSyntax attribute in siblingMethod.AttributeLists.SelectMany(static list => list.Attributes))
            {
                SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(attribute, cancellationToken);
                if (symbolInfo.Symbol is not IMethodSymbol ctor
                    || ctor.ContainingType.Name != "PacketOpcodeAttribute")
                {
                    continue;
                }

                if (attribute.ArgumentList?.Arguments.Count != 1)
                {
                    continue;
                }

                Optional<object?> constantValue = semanticModel.GetConstantValue(attribute.ArgumentList.Arguments[0].Expression, cancellationToken);
                if (constantValue.HasValue && constantValue.Value is ushort ushortValue)
                {
                    used[ushortValue] = true;
                }
                else if (constantValue.HasValue && constantValue.Value is int intValue && intValue >= ushort.MinValue && intValue <= ushort.MaxValue)
                {
                    used[(ushort)intValue] = true;
                }
            }
        }

        for (ushort candidate = 0; candidate < ushort.MaxValue; candidate++)
        {
            if (!used[candidate])
            {
                return candidate;
            }
        }

        return ushort.MaxValue;
    }
}
