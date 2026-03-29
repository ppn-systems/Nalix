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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PacketRegistryDeserializerCodeFixProvider)), Shared]
public sealed class PacketRegistryDeserializerCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add Deserialize(ReadOnlySpan<byte>)";
    private const string EquivalenceKey = "Nalix.PacketRegistry.Deserialize.Add";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX009"];

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
        if (invocation is null)
        {
            return;
        }

        TypeDeclarationSyntax? typeDeclaration = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();
        if (typeDeclaration is null)
        {
            return;
        }

        string typeName = ExtractQuotedName(diagnostic.GetMessage()) ?? typeDeclaration.Identifier.ValueText;
        TypeDeclarationSyntax? targetType = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.ValueText == typeName);
        if (targetType is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => AddDeserializeMethodAsync(context.Document, targetType, typeName, cancellationToken),
                equivalenceKey: EquivalenceKey),
            diagnostic);
    }

    private static async Task<Document> AddDeserializeMethodAsync(Document document, TypeDeclarationSyntax typeDeclaration, string typeName, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        MethodDeclarationSyntax method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.IdentifierName(typeName),
                SyntaxFactory.Identifier("Deserialize"))
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.NewKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("buffer"))
                    .WithType(
                        SyntaxFactory.GenericName("ReadOnlySpan")
                            .WithTypeArgumentList(
                                SyntaxFactory.TypeArgumentList(
                                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ByteKeyword)))))))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.GenericName("PacketBase")
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(SyntaxFactory.IdentifierName(typeName)))),
                                SyntaxFactory.IdentifierName("Deserialize")))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("buffer")))))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        editor.InsertMembers(typeDeclaration, typeDeclaration.Members.Count, [method]);
        return editor.GetChangedDocument();
    }

    private static string? ExtractQuotedName(string message)
    {
        int first = message.IndexOf('\'');
        int second = first >= 0 ? message.IndexOf('\'', first + 1) : -1;
        return first >= 0 && second > first ? message.Substring(first + 1, second - first - 1) : null;
    }
}
