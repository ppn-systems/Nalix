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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConfigurationIgnoreCodeFixProvider)), Shared]
public sealed class ConfigurationIgnoreCodeFixProvider : CodeFixProvider
{
    private const string AddConfigurationIgnoreTitle = "Add [ConfiguredIgnore]";
    private const string AddConfigurationIgnoreEquivalenceKey = "Nalix.Configuration.ConfiguredIgnore.Add";
    private const string MakeSetterPublicTitle = "Make setter public";
    private const string MakeSetterPublicEquivalenceKey = "Nalix.Configuration.Setter.MakePublic";

    public override ImmutableArray<string> FixableDiagnosticIds => ["NALIX023", "NALIX024"];

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
        PropertyDeclarationSyntax? property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        if (property is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: AddConfigurationIgnoreTitle,
                createChangedDocument: cancellationToken => AddConfigurationIgnoreAsync(context.Document, property, cancellationToken),
                equivalenceKey: AddConfigurationIgnoreEquivalenceKey),
            diagnostic);

        if (diagnostic.Id == "NALIX024" && CanMakeSetterPublic(property))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: MakeSetterPublicTitle,
                    createChangedDocument: cancellationToken => MakeSetterPublicAsync(context.Document, property, cancellationToken),
                    equivalenceKey: MakeSetterPublicEquivalenceKey),
                diagnostic);
        }
    }

    private static async Task<Document> AddConfigurationIgnoreAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(
                    SyntaxFactory.ParseName("Nalix.Abstractions.ConfiguredIgnore"))));

        editor.ReplaceNode(property, property.AddAttributeLists(attributeList));
        return editor.GetChangedDocument();
    }

    private static bool CanMakeSetterPublic(PropertyDeclarationSyntax property)
        => property.AccessorList is not null
           && property.AccessorList.Accessors.Count > 0;

    private static async Task<Document> MakeSetterPublicAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        PropertyDeclarationSyntax updatedProperty;

        if (property.AccessorList is null)
        {
            return document;
        }

        AccessorDeclarationSyntax[] accessors = [.. property.AccessorList.Accessors];
        int setterIndex = System.Array.FindIndex(accessors, static accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration));

        if (setterIndex >= 0)
        {
            AccessorDeclarationSyntax setter = accessors[setterIndex];
            AccessorDeclarationSyntax updatedSetter = setter.WithModifiers(default);
            accessors[setterIndex] = updatedSetter;
            updatedProperty = property.WithAccessorList(property.AccessorList.WithAccessors(SyntaxFactory.List(accessors)));
        }
        else
        {
            AccessorDeclarationSyntax getter = accessors[0];
            AccessorDeclarationSyntax setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            updatedProperty = property.WithAccessorList(
                property.AccessorList.WithAccessors(
                    SyntaxFactory.List([getter, setter])));
        }

        editor.ReplaceNode(property, updatedProperty);
        return editor.GetChangedDocument();
    }
}
