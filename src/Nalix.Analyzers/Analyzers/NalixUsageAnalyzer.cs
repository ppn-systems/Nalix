// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Nalix.Analyzers.Diagnostics;

namespace Nalix.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class NalixUsageAnalyzer : DiagnosticAnalyzer
{
    private const int UnusuallyLargeSerializeOrderGapThreshold = 16;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [
            DiagnosticDescriptors.DuplicateControllerOpcode,
            DiagnosticDescriptors.ControllerMethodRequiresOpcode,
            DiagnosticDescriptors.InvalidControllerHandlerSignature,
            DiagnosticDescriptors.PacketContextTypeMismatch,
            DiagnosticDescriptors.HandlerPacketTypeMismatch,
            DiagnosticDescriptors.MiddlewareTypeMismatch,
            DiagnosticDescriptors.BufferMiddlewareShouldNotUseStageAttribute,
            DiagnosticDescriptors.ControllerMissingPacketControllerAttribute,
            DiagnosticDescriptors.PacketRegistryPacketMissingDeserializer,
            DiagnosticDescriptors.PacketBaseSelfTypeMismatch,
            DiagnosticDescriptors.PacketDeserializerSelfTypeMismatch,
            DiagnosticDescriptors.PacketBaseMissingDeserializeMethod,
            DiagnosticDescriptors.ExplicitSerializationMemberMissingOrder,
            DiagnosticDescriptors.DuplicateSerializeOrder,
            DiagnosticDescriptors.SerializeIgnoreConflictsWithOrder,
            DiagnosticDescriptors.SerializeDynamicSizeOnFixedMember,
            DiagnosticDescriptors.PacketDeserializeSignatureInvalid,
            DiagnosticDescriptors.PacketRegistryPacketMustBeConcrete,
            DiagnosticDescriptors.BufferMiddlewareRegistrationTypeMismatch,
            DiagnosticDescriptors.ResetForPoolShouldCallBase,
            DiagnosticDescriptors.NegativeSerializeOrder,
            DiagnosticDescriptors.PacketMemberOverlapsHeaderRegion,
            DiagnosticDescriptors.UnsupportedConfigurationPropertyType,
            DiagnosticDescriptors.ConfigurationPropertyNotBindable,
            DiagnosticDescriptors.MetadataProviderClearsOpcode,
            DiagnosticDescriptors.MetadataProviderOverwritesOpcodeWithoutGuard,
            DiagnosticDescriptors.RequestOptionsRetryCountNegative,
            DiagnosticDescriptors.RequestOptionsTimeoutNegative,
            DiagnosticDescriptors.RequestEncryptRequiresTcpSession,
            DiagnosticDescriptors.PacketMiddlewareMissingOrder,
            DiagnosticDescriptors.BufferMiddlewareMissingOrder,
            DiagnosticDescriptors.InboundMiddlewareAlwaysExecuteIgnored,
            DiagnosticDescriptors.MiddlewareRegistrationDuplicateOrder,
            DiagnosticDescriptors.SerializeHeaderConflictsWithOrder,
            DiagnosticDescriptors.ReservedOpCodeRange,
            DiagnosticDescriptors.GlobalDuplicateOpcode,
            DiagnosticDescriptors.AllocationInHotPath,
            DiagnosticDescriptors.OpCodeDocMismatch,
            DiagnosticDescriptors.PotentialBufferLeaseLeak,
            DiagnosticDescriptors.NetworkHostingMissingBufferPoolManager,
            DiagnosticDescriptors.NetworkHostingMissingConnectionHub,
            DiagnosticDescriptors.NetworkHostingHandlerTypeInvalid,
            DiagnosticDescriptors.NetworkHostingMetadataProviderTypeInvalid,
            DiagnosticDescriptors.NetworkHostingMissingTcpBinding,
            DiagnosticDescriptors.NetworkHostingUdpWithoutTcpBinding,
            DiagnosticDescriptors.UnusuallyLargeSerializeOrderGap,
            DiagnosticDescriptors.DispatchLoopCountOutOfRange,
            DiagnosticDescriptors.UnsupportedControllerHandlerReturnType,
            DiagnosticDescriptors.PacketOpcodeOnNonControllerType,
            DiagnosticDescriptors.FixedSizeSerializableHasDynamicMember,
            DiagnosticDescriptors.PacketDeserializeSpanOverloadMissing,
            DiagnosticDescriptors.RequestEncryptVariableRequiresTcpSession,
            DiagnosticDescriptors.DuplicatePacketControllerName,
            DiagnosticDescriptors.RedundantPacketContextPacketCast,
            DiagnosticDescriptors.MiddlewareRegistrationNullLiteral,
            DiagnosticDescriptors.RequestOptionsInfiniteTimeoutWithRetry,
            DiagnosticDescriptors.GenericPacketHandlerMethod
        ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            SymbolSet? symbols = SymbolSet.Create(startContext.Compilation);
            if (symbols is null)
            {
                return;
            }

            ConcurrentDictionary<ushort, (IMethodSymbol Method, INamedTypeSymbol Controller)> globalOpcodes = new();
            ConcurrentDictionary<string, INamedTypeSymbol> controllerNames = new(StringComparer.OrdinalIgnoreCase);

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, symbols, globalOpcodes, controllerNames),
                SymbolKind.NamedType);

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, symbols),
                SymbolKind.Method);

            startContext.RegisterOperationAction(
                operationContext => AnalyzeObjectCreation(operationContext, symbols),
                OperationKind.ObjectCreation);

            startContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, symbols),
                OperationKind.Invocation);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeMethodDeclaration(syntaxContext, symbols),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.MethodDeclaration);
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        SymbolSet symbols,
        ConcurrentDictionary<ushort, (IMethodSymbol Method, INamedTypeSymbol Controller)> globalOpcodes,
        ConcurrentDictionary<string, INamedTypeSymbol> controllerNames)
    {
        INamedTypeSymbol typeSymbol = (INamedTypeSymbol)context.Symbol;

        if (typeSymbol.TypeKind is not (TypeKind.Class or TypeKind.Struct) || typeSymbol.IsAbstract || typeSymbol.IsImplicitlyDeclared)
        {
            return;
        }

        AnalyzePacketType(context, typeSymbol, symbols);
        AnalyzeSerializationType(context, typeSymbol, symbols);
        AnalyzeConfigurationType(context, typeSymbol, symbols);
        AnalyzeMetadataProviderType(context, typeSymbol, symbols);
        AnalyzeMiddlewareType(context, typeSymbol, symbols);
 
        if (HasAttribute(typeSymbol, symbols.ControllerAttribute))
        {
            AnalyzeControllerType(context, typeSymbol, symbols, globalOpcodes, controllerNames);
        }
    }

    private static void AnalyzeConfigurationType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        if (!HasBaseType(typeSymbol, symbols.ConfigurationLoaderType))
        {
            return;
        }

        foreach (IPropertySymbol property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsImplicitlyDeclared
                || property.DeclaredAccessibility != Accessibility.Public
                || property.IsStatic
                || HasAttribute(property, symbols.ConfiguredIgnoreAttribute))
            {
                continue;
            }

            if (property.SetMethod is null)
            {
                Report(
                    context,
                    DiagnosticDescriptors.ConfigurationPropertyNotBindable,
                    property,
                    property.Name,
                    typeSymbol.Name,
                    "does not declare a setter");
                continue;
            }

            if (property.SetMethod.DeclaredAccessibility != Accessibility.Public)
            {
                Report(
                    context,
                    DiagnosticDescriptors.ConfigurationPropertyNotBindable,
                    property,
                    property.Name,
                    typeSymbol.Name,
                    "does not have a public setter");
                continue;
            }

            if (!IsSupportedConfigurationType(property.Type))
            {
                Report(
                    context,
                    DiagnosticDescriptors.UnsupportedConfigurationPropertyType,
                    property,
                    property.Name,
                    typeSymbol.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }
    }

    private static void AnalyzeMetadataProviderType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        if (!Implements(typeSymbol, symbols.PacketMetadataProviderType))
        {
            return;
        }

        foreach (IMethodSymbol method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.Name != "Populate"
                || method.MethodKind != MethodKind.Ordinary
                || method.Parameters.Length != 2
                || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, symbols.MethodInfoType)
                || !SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, symbols.PacketMetadataBuilderType))
            {
                continue;
            }

            foreach (SyntaxReference syntaxReference in method.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax methodSyntax)
                {
                    continue;
                }

                AnalyzePopulateMethod(context, typeSymbol, method, methodSyntax, symbols);
            }
        }
    }

    private static void AnalyzeMiddlewareType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        if (!ImplementsOpenGeneric(typeSymbol, symbols.PacketMiddlewareType))
        {
            return;
        }

        AttributeData? orderAttribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.MiddlewareOrderAttribute));
        AttributeData? stageAttribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.MiddlewareStageAttribute));

        if (orderAttribute is null)
        {
            Report(context, DiagnosticDescriptors.PacketMiddlewareMissingOrder, typeSymbol, typeSymbol.Name);
        }

        if (stageAttribute is not null && IsInboundAlwaysExecute(stageAttribute, symbols.MiddlewareStageType))
        {
            Report(context, DiagnosticDescriptors.InboundMiddlewareAlwaysExecuteIgnored, typeSymbol, typeSymbol.Name);
        }
    }

    private static void AnalyzePopulateMethod(
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        IMethodSymbol methodSymbol,
        MethodDeclarationSyntax methodSyntax,
        SymbolSet symbols)
    {
        SyntaxNode? bodyRoot = (SyntaxNode?)methodSyntax.Body ?? methodSyntax.ExpressionBody?.Expression;
        if (bodyRoot is null)
        {
            return;
        }

        string builderParameterName = methodSymbol.Parameters[1].Name;
        bool hasOpcodeGuard = bodyRoot.DescendantNodesAndSelf().Any(node => IsOpcodeGuard(node, builderParameterName));
        bool reportedOverwrite = false;

        foreach (AssignmentExpressionSyntax assignment in bodyRoot.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>())
        {
            if (!IsBuilderOpcodeAccess(assignment.Left, builderParameterName))
            {
                continue;
            }

            if (assignment.Right.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MetadataProviderClearsOpcode,
                    assignment.GetLocation(),
                    typeSymbol.Name));
                continue;
            }

            if (!hasOpcodeGuard && !reportedOverwrite)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MetadataProviderOverwritesOpcodeWithoutGuard,
                    assignment.GetLocation(),
                    typeSymbol.Name));
                reportedOverwrite = true;
            }
        }
    }

    private static void AnalyzeSerializationType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        AttributeData? serializePackable = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.SerializePackableAttribute));

        if (serializePackable is null)
        {
            return;
        }

        bool isExplicitLayout = IsExplicitSerializeLayout(serializePackable, symbols.SerializeLayoutType);
        bool isPacketBaseType = InheritsPacketBase(typeSymbol, symbols);
        List<(ISymbol Member, int Order)> orderedMembers = [];
        List<(ISymbol Member, int Order)> serializeOrderedMembers = [];

        foreach (ISymbol member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol and not IFieldSymbol)
            {
                continue;
            }

            if (member.IsImplicitlyDeclared || member.IsStatic)
            {
                continue;
            }

            bool hasIgnore = HasAttribute(member, symbols.SerializeIgnoreAttribute);
            int? headerOrder = GetSerializeOrder(member, symbols.SerializeHeaderAttribute);
            int? order = GetSerializeOrder(member, symbols.SerializeOrderAttribute);
            bool hasDynamic = HasAttribute(member, symbols.SerializeDynamicSizeAttribute);
            ITypeSymbol memberType = member is IPropertySymbol property ? property.Type : ((IFieldSymbol)member).Type;

            if (hasIgnore && order.HasValue)
            {
                Report(context, DiagnosticDescriptors.SerializeIgnoreConflictsWithOrder, member, member.Name);
            }

            if (headerOrder.HasValue && order.HasValue)
            {
                Report(
                    context,
                    DiagnosticDescriptors.SerializeHeaderConflictsWithOrder,
                    member,
                    member.Name);
            }

            int? finalOrder = headerOrder ?? order;

            if (finalOrder.HasValue)
            {
                orderedMembers.Add((member, finalOrder.Value));
                if (order.HasValue)
                {
                    serializeOrderedMembers.Add((member, order.Value));
                }

                if (finalOrder.Value < 0)
                {
                    Report(context, DiagnosticDescriptors.NegativeSerializeOrder, member, member.Name, finalOrder.Value);
                }

                if (isExplicitLayout
                    && isPacketBaseType
                    && order.HasValue
                    && order.Value < symbols.PacketHeaderRegionOffset)
                {
                    Report(
                        context,
                        DiagnosticDescriptors.PacketMemberOverlapsHeaderRegion,
                        member,
                        member.Name,
                        order.Value,
                        symbols.PacketHeaderRegionOffset);
                }

            }
            else if (isExplicitLayout && !hasIgnore)
            {
                Report(context, DiagnosticDescriptors.ExplicitSerializationMemberMissingOrder, member, member.Name);
            }

            if (hasDynamic && IsFixedSizeSerializationType(memberType))
            {
                Report(
                    context,
                    DiagnosticDescriptors.SerializeDynamicSizeOnFixedMember,
                    member,
                    member.Name,
                    memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }

        foreach (IGrouping<int, (ISymbol Member, int Order)> duplicateGroup in orderedMembers.GroupBy(static x => x.Order).Where(static g => g.Count() > 1))
        {
            foreach ((ISymbol member, _) in duplicateGroup)
            {
                Report(
                    context,
                    DiagnosticDescriptors.DuplicateSerializeOrder,
                    member,
                    member.Name,
                    duplicateGroup.Key,
                    typeSymbol.Name);
            }
        }

        bool hasDuplicateSerializeOrder = serializeOrderedMembers
            .GroupBy(static x => x.Order)
            .Any(static g => g.Count() > 1);

        if (hasDuplicateSerializeOrder)
        {
            return;
        }

        List<(ISymbol Member, int Order)> sortedSerializeOrders = [.. serializeOrderedMembers.OrderBy(static x => x.Order)];
        for (int i = 1; i < sortedSerializeOrders.Count; i++)
        {
            (ISymbol currentMember, int currentOrder) = sortedSerializeOrders[i];
            int previousOrder = sortedSerializeOrders[i - 1].Order;
            int gap = currentOrder - previousOrder;
            if (gap >= UnusuallyLargeSerializeOrderGapThreshold)
            {
                Report(
                    context,
                    DiagnosticDescriptors.UnusuallyLargeSerializeOrderGap,
                    currentMember,
                    currentMember.Name,
                    currentOrder,
                    previousOrder);
            }
        }
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context, SymbolSet symbols)
    {
        MethodDeclarationSyntax methodDeclaration = (MethodDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        AnalyzeRedundantPacketContextPacketCast(context, methodDeclaration, methodSymbol, symbols);

        if (methodDeclaration.Identifier.ValueText != "ResetForPool"
            || methodDeclaration.ParameterList.Parameters.Count != 0
            || !methodSymbol.IsOverride
            || methodSymbol.ContainingType is not INamedTypeSymbol containingType
            || !InheritsPacketBase(containingType, symbols))
        {
            return;
        }

        bool callsBaseReset = methodDeclaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax
                {
                    Expression: BaseExpressionSyntax,
                    Name.Identifier.ValueText: "ResetForPool"
                });

        if (!callsBaseReset)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ResetForPoolShouldCallBase,
                methodDeclaration.Identifier.GetLocation(),
                containingType.Name));
        }
    }

    private static void AnalyzeRedundantPacketContextPacketCast(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        SymbolSet symbols)
    {
        if (methodSymbol.Parameters.Length == 0)
        {
            return;
        }

        if (methodSymbol.Parameters[0].Type is not INamedTypeSymbol firstParamType
            || !firstParamType.IsGenericType
            || !SymbolEqualityComparer.Default.Equals(firstParamType.ConstructedFrom, symbols.PacketContextType)
            || firstParamType.TypeArguments.Length != 1)
        {
            return;
        }

        ITypeSymbol packetType = firstParamType.TypeArguments[0];
        string contextParamName = methodSymbol.Parameters[0].Name;

        foreach (CastExpressionSyntax cast in methodDeclaration.DescendantNodes().OfType<CastExpressionSyntax>())
        {
            ITypeSymbol? castType = context.SemanticModel.GetTypeInfo(cast.Type, context.CancellationToken).Type;
            if (castType is null || !SymbolEqualityComparer.Default.Equals(castType, packetType))
            {
                continue;
            }

            if (cast.Expression is MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: var identifierName },
                    Name.Identifier.ValueText: "Packet"
                }
                && identifierName == contextParamName)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.RedundantPacketContextPacketCast,
                    cast.GetLocation(),
                    methodSymbol.Name,
                    packetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    contextParamName));
            }
        }
    }

    private static void AnalyzePacketType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        INamedTypeSymbol? baseType = typeSymbol.BaseType;
        bool isPacketBaseSelfType = false;
        if (baseType is not null
            && baseType.IsGenericType
            && SymbolEqualityComparer.Default.Equals(baseType.ConstructedFrom, symbols.PacketBaseType))
        {
            ITypeSymbol selfTypeArgument = baseType.TypeArguments[0];
            if (!SymbolEqualityComparer.Default.Equals(selfTypeArgument, typeSymbol))
            {
                Location? location = typeSymbol.Locations.FirstOrDefault(static l => l.IsInSource);
                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.PacketBaseSelfTypeMismatch,
                        location,
                        typeSymbol.Name,
                        selfTypeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
            else
            {
                isPacketBaseSelfType = true;
            }
        }

        foreach (INamedTypeSymbol implementedInterface in typeSymbol.AllInterfaces.OfType<INamedTypeSymbol>())
        {
            if (!implementedInterface.IsGenericType
                || !SymbolEqualityComparer.Default.Equals(implementedInterface.ConstructedFrom, symbols.PacketDeserializerType))
            {
                continue;
            }

            ITypeSymbol selfTypeArgument = implementedInterface.TypeArguments[0];
            if (!SymbolEqualityComparer.Default.Equals(selfTypeArgument, typeSymbol))
            {
                Location? location = typeSymbol.Locations.FirstOrDefault(static l => l.IsInSource);
                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.PacketDeserializerSelfTypeMismatch,
                        location,
                        typeSymbol.Name,
                        selfTypeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
        }

        if (isPacketBaseSelfType)
        {
            AnalyzePacketBaseDeserializeContract(context, typeSymbol);
        }

        AnalyzeFixedSizeSerializableDynamics(context, typeSymbol, symbols);
        AnalyzeDeserializeOverloads(context, typeSymbol, symbols);
    }

    private static void AnalyzeControllerType(
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        SymbolSet symbols,
        ConcurrentDictionary<ushort, (IMethodSymbol Method, INamedTypeSymbol Controller)> globalOpcodes,
        ConcurrentDictionary<string, INamedTypeSymbol> controllerNames)
    {
        string controllerName = GetPacketControllerName(typeSymbol, symbols.ControllerAttribute) ?? typeSymbol.Name;
        if (controllerNames.TryGetValue(controllerName, out INamedTypeSymbol? existing)
            && !SymbolEqualityComparer.Default.Equals(existing, typeSymbol))
        {
            Report(
                context,
                DiagnosticDescriptors.DuplicatePacketControllerName,
                typeSymbol,
                controllerName,
                existing.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
        else
        {
            controllerNames[controllerName] = typeSymbol;
        }

        ImmutableArray<IMethodSymbol> methods = [.. typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method => method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && !method.IsImplicitlyDeclared)];

        foreach (IGrouping<ushort, IMethodSymbol> duplicateGroup in methods
                     .Select(method => (Method: method, Opcode: GetOpcode(method, symbols.PacketOpcodeAttribute)))
                     .Where(entry => entry.Opcode.HasValue)
                     .GroupBy(entry => entry.Opcode!.Value, entry => entry.Method)
                     .Where(group => group.Count() > 1))
        {
            foreach (IMethodSymbol duplicateMethod in duplicateGroup)
            {
                Location? location = duplicateMethod.Locations.FirstOrDefault(static l => l.IsInSource);
                if (location is null)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateControllerOpcode,
                    location,
                    duplicateMethod.Name,
                    duplicateGroup.Key));
            }
        }

        foreach (IMethodSymbol methodSymbol in methods)
        {
            if (methodSymbol.IsOverride)
            {
                continue;
            }

            ushort? opcode = GetOpcode(methodSymbol, symbols.PacketOpcodeAttribute);
            bool isCandidate = IsNalixHandlerCandidate(methodSymbol, symbols);

            if (opcode.HasValue)
            {
                if (methodSymbol.IsGenericMethod)
                {
                    Report(context, DiagnosticDescriptors.GenericPacketHandlerMethod, methodSymbol, methodSymbol.Name);
                }

                if (opcode.Value < 0x0100 && !HasInternalByPass(methodSymbol, typeSymbol, symbols))
                {
                    Report(context, DiagnosticDescriptors.ReservedOpCodeRange, methodSymbol, methodSymbol.Name, opcode.Value);
                }

                if (globalOpcodes.TryGetValue(opcode.Value, out (IMethodSymbol Method, INamedTypeSymbol Controller) existingOpcode) && !SymbolEqualityComparer.Default.Equals(existingOpcode.Controller, typeSymbol))
                {
                    Report(context, DiagnosticDescriptors.GlobalDuplicateOpcode, methodSymbol, methodSymbol.Name, opcode.Value, existingOpcode.Method.Name, existingOpcode.Controller.Name);
                }
                else
                {
                    globalOpcodes[opcode.Value] = (methodSymbol, typeSymbol);
                }

                if (!HasSupportedParameterSignature(methodSymbol, symbols))
                {
                    Report(context, DiagnosticDescriptors.InvalidControllerHandlerSignature, methodSymbol, methodSymbol.Name);
                }
                else
                {
                    ReportPacketContextMismatchIfAny(context, methodSymbol, symbols);
                }

                if (!HasSupportedReturnType(methodSymbol.ReturnType, symbols))
                {
                    Report(
                        context,
                        DiagnosticDescriptors.UnsupportedControllerHandlerReturnType,
                        methodSymbol,
                        methodSymbol.Name,
                        methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                }
            }
            else if (isCandidate)
            {
                Report(context, DiagnosticDescriptors.ControllerMethodRequiresOpcode, methodSymbol, methodSymbol.Name);
            }
        }
    }

    private static bool HasSupportedParameterSignature(IMethodSymbol methodSymbol, SymbolSet symbols)
    {
        ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;
        if (parameters.Length == 0)
        {
            return false;
        }

        ITypeSymbol firstParameterType = parameters[0].Type;
        if (IsPacketContext(firstParameterType, symbols))
        {
            return parameters.Length switch
            {
                1 => true,
                2 => SymbolEqualityComparer.Default.Equals(parameters[1].Type, symbols.CancellationTokenType),
                _ => false
            };
        }

        if (Implements(firstParameterType, symbols.PacketInterface))
        {
            if (parameters.Length < 2 || !Implements(parameters[1].Type, symbols.ConnectionType))
            {
                return false;
            }

            return parameters.Length switch
            {
                2 => true,
                3 => SymbolEqualityComparer.Default.Equals(parameters[2].Type, symbols.CancellationTokenType),
                _ => false
            };
        }

        return false;
    }

    private static bool HasSupportedReturnType(ITypeSymbol returnType, SymbolSet symbols)
    {
        if (returnType.SpecialType == SpecialType.System_Void)
        {
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(returnType, symbols.TaskType)
            || SymbolEqualityComparer.Default.Equals(returnType, symbols.ValueTaskType))
        {
            return true;
        }

        if (Implements(returnType, symbols.PacketInterface))
        {
            return true;
        }

        if (returnType is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
        {
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(returnType, symbols.MemoryByteType)
            || SymbolEqualityComparer.Default.Equals(returnType, symbols.ReadOnlyMemoryByteType))
        {
            return true;
        }

        if (returnType is INamedTypeSymbol namedReturnType && namedReturnType.IsGenericType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedReturnType.ConstructedFrom, symbols.GenericTaskType)
                || SymbolEqualityComparer.Default.Equals(namedReturnType.ConstructedFrom, symbols.GenericValueTaskType))
            {
                return namedReturnType.TypeArguments.Length == 1
                    && HasSupportedReturnType(namedReturnType.TypeArguments[0], symbols);
            }
        }

        return false;
    }

    private static void AnalyzeFixedSizeSerializableDynamics(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        if (!Implements(typeSymbol, symbols.FixedSizeSerializableType))
        {
            return;
        }

        foreach (ISymbol member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol and not IFieldSymbol
                || member.IsStatic
                || member.IsImplicitlyDeclared
                || HasAttribute(member, symbols.SerializeIgnoreAttribute))
            {
                continue;
            }

            ITypeSymbol memberType = member is IPropertySymbol p ? p.Type : ((IFieldSymbol)member).Type;
            if (IsDynamicSizeType(memberType, symbols))
            {
                Report(
                    context,
                    DiagnosticDescriptors.FixedSizeSerializableHasDynamicMember,
                    member,
                    typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    member.Name,
                    memberType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }
    }

    private static void AnalyzeDeserializeOverloads(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        // NALIX052 is a packet-shape rule. Skip utility/static helper types that
        // happen to expose Deserialize overloads (for example LiteSerializer).
        bool isPacketLikeType =
            InheritsPacketBase(typeSymbol, symbols)
            || Implements(typeSymbol, symbols.PacketInterface)
            || typeSymbol.AllInterfaces
                .OfType<INamedTypeSymbol>()
                .Any(interfaceSymbol =>
                    interfaceSymbol.IsGenericType
                    && SymbolEqualityComparer.Default.Equals(interfaceSymbol.ConstructedFrom, symbols.PacketDeserializerType));

        if (!isPacketLikeType)
        {
            return;
        }

        bool hasAnyDeserialize = false;
        bool hasReadOnlySpanDeserialize = false;

        foreach (IMethodSymbol method in EnumerateDeserializeMethods(typeSymbol))
        {
            if (!method.IsStatic
                || method.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            hasAnyDeserialize = true;

            if (IsValidPacketDeserializeSignature(method, typeSymbol))
            {
                hasReadOnlySpanDeserialize = true;
                break;
            }
        }

        if (hasAnyDeserialize && !hasReadOnlySpanDeserialize)
        {
            Report(context, DiagnosticDescriptors.PacketDeserializeSpanOverloadMissing, typeSymbol, typeSymbol.Name);
        }
    }

    private static void AnalyzePacketBaseDeserializeContract(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        ImmutableArray<IMethodSymbol> declaredSpanDeserializeMethods = [.. typeSymbol.GetMembers("Deserialize")
            .OfType<IMethodSymbol>()
            .Where(static method =>
                method.MethodKind == MethodKind.Ordinary
                && IsReadOnlySpanByteType(method.Parameters.FirstOrDefault()?.Type))];

        if (declaredSpanDeserializeMethods.Length > 0)
        {
            if (declaredSpanDeserializeMethods.Any(method => IsValidPacketDeserializeSignature(method, typeSymbol)))
            {
                return;
            }

            Report(context, DiagnosticDescriptors.PacketDeserializeSignatureInvalid, typeSymbol, typeSymbol.Name);
            return;
        }

        foreach (IMethodSymbol method in EnumerateDeserializeMethods(typeSymbol))
        {
            if (IsValidPacketDeserializeSignature(method, typeSymbol))
            {
                return;
            }
        }

        Report(context, DiagnosticDescriptors.PacketBaseMissingDeserializeMethod, typeSymbol, typeSymbol.Name);
    }

    private static bool IsValidPacketDeserializeSignature(IMethodSymbol method, INamedTypeSymbol containingType)
        => method.IsStatic
           && method.DeclaredAccessibility == Accessibility.Public
           && method.Parameters.Length == 1
           && IsReadOnlySpanByteType(method.Parameters[0].Type)
           && SymbolEqualityComparer.Default.Equals(method.ReturnType, containingType);

    private static IEnumerable<IMethodSymbol> EnumerateDeserializeMethods(INamedTypeSymbol typeSymbol)
    {
        for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.BaseType)
        {
            foreach (IMethodSymbol method in current.GetMembers("Deserialize")
                .OfType<IMethodSymbol>()
                .Where(static method => method.MethodKind == MethodKind.Ordinary))
            {
                yield return method;
            }
        }
    }

    private static bool IsReadOnlySpanByteType(ITypeSymbol? type)
        => type is INamedTypeSymbol parameterType
           && parameterType.OriginalDefinition.MetadataName == "ReadOnlySpan`1"
           && parameterType.OriginalDefinition.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System"
           && parameterType.TypeArguments.Length == 1
           && parameterType.TypeArguments[0].SpecialType == SpecialType.System_Byte;

    private static bool IsDynamicSizeType(ITypeSymbol typeSymbol, SymbolSet symbols)
    {
        if (typeSymbol.SpecialType == SpecialType.System_String
            || typeSymbol is IArrayTypeSymbol
            || Implements(typeSymbol, symbols.PacketInterface))
        {
            return true;
        }

        return false;
    }

    private static string? GetPacketControllerName(INamedTypeSymbol typeSymbol, INamedTypeSymbol? controllerAttributeSymbol)
    {
        AttributeData? attribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, controllerAttributeSymbol));
        if (attribute is null)
        {
            return null;
        }

        if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string ctorName && !string.IsNullOrWhiteSpace(ctorName))
        {
            return ctorName;
        }

        return attribute.NamedArguments
            .Where(namedArg => namedArg.Key == "Name" && namedArg.Value.Value is string namedName && !string.IsNullOrWhiteSpace(namedName))
            .Select(namedArg => (string)namedArg.Value.Value!)
            .FirstOrDefault();
    }

    private static bool IsNalixHandlerCandidate(IMethodSymbol methodSymbol, SymbolSet symbols)
    {
        if (methodSymbol.Parameters.Length is < 1 or > 3)
        {
            return false;
        }

        ITypeSymbol firstParameterType = methodSymbol.Parameters[0].Type;
        return Implements(firstParameterType, symbols.PacketInterface) || IsPacketContext(firstParameterType, symbols);
    }

    private static bool TryGetPacketContextType(IMethodSymbol methodSymbol, INamedTypeSymbol? packetContextSymbol, out ITypeSymbol? packetType)
    {
        packetType = null;

        if (methodSymbol.Parameters.Length == 0)
        {
            return false;
        }

        ITypeSymbol firstParameterType = methodSymbol.Parameters[0].Type;
        if (firstParameterType is not INamedTypeSymbol namedType
            || !namedType.IsGenericType
            || !SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, packetContextSymbol))
        {
            return false;
        }

        packetType = namedType.TypeArguments[0];
        return true;
    }

    private static ITypeSymbol? GetLegacyPacketType(IMethodSymbol methodSymbol, SymbolSet symbols)
    {
        if (methodSymbol.Parameters.Length == 0)
        {
            return null;
        }

        ITypeSymbol firstParameterType = methodSymbol.Parameters[0].Type;
        if (IsPacketContext(firstParameterType, symbols))
        {
            return null;
        }

        return Implements(firstParameterType, symbols.PacketInterface) ? firstParameterType : null;
    }

    private static ushort? GetOpcode(IMethodSymbol methodSymbol, INamedTypeSymbol? packetAttributeSymbol)
    {
        AttributeData? attribute = methodSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, packetAttributeSymbol));

        if (attribute?.ConstructorArguments.Length != 1)
        {
            return null;
        }

        object? value = attribute.ConstructorArguments[0].Value;
        return value switch
        {
            ushort u => u,
            int i => (ushort)i,
            _ => null
        };
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeSymbol)
        => attributeSymbol is not null && symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol));

    private static bool IsPacketContext(ITypeSymbol? type, SymbolSet symbols)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (!namedType.IsGenericType)
        {
            return false;
        }

        INamedTypeSymbol genericDef = namedType.ConstructedFrom;
        return SymbolEqualityComparer.Default.Equals(genericDef, symbols.PacketContextType)
               || SymbolEqualityComparer.Default.Equals(genericDef, symbols.PacketContextInterface);
    }

    private static bool HasInternalByPass(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        return HasAttribute(methodSymbol, symbols.ReservedOpcodePermittedAttribute)
               || HasAttribute(typeSymbol, symbols.ReservedOpcodePermittedAttribute);
    }

    private static bool Implements(ITypeSymbol typeSymbol, INamedTypeSymbol? interfaceSymbol)
        => interfaceSymbol is not null && (SymbolEqualityComparer.Default.Equals(typeSymbol, interfaceSymbol)
           || typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol)));

    private static bool ImplementsOpenGeneric(ITypeSymbol typeSymbol, INamedTypeSymbol? openGenericInterfaceSymbol)
        => openGenericInterfaceSymbol is not null && typeSymbol.AllInterfaces
            .OfType<INamedTypeSymbol>()
            .Any(i => i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.ConstructedFrom, openGenericInterfaceSymbol));

    private static bool IsAssignable(ITypeSymbol from, ITypeSymbol? to)
        => to is not null && (SymbolEqualityComparer.Default.Equals(from, to)
           || from.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, to))
           || HasBaseType(from, to));

    private static bool HasBaseType(ITypeSymbol typeSymbol, ITypeSymbol? expectedBase)
    {
        if (expectedBase is null)
        {
            return false;
        }

        ITypeSymbol? current = typeSymbol.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, expectedBase))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool InheritsPacketBase(INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        INamedTypeSymbol? current = typeSymbol.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && SymbolEqualityComparer.Default.Equals(current.ConstructedFrom, symbols.PacketBaseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsExplicitSerializeLayout(AttributeData serializePackable, INamedTypeSymbol? serializeLayoutType)
    {
        if (serializePackable.ConstructorArguments.Length != 1)
        {
            return false;
        }

        TypedConstant arg = serializePackable.ConstructorArguments[0];
        if (arg.Type is null
            || serializeLayoutType is null
            || !SymbolEqualityComparer.Default.Equals(arg.Type, serializeLayoutType)
            || arg.Value is null)
        {
            return false;
        }

        // Resolve by enum member value instead of hardcoded numeric constants.
        object? explicitValue = serializeLayoutType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(static field => field.Name == "Explicit")
            ?.ConstantValue;

        return explicitValue is not null
               && Equals(Convert.ToInt32(arg.Value, CultureInfo.InvariantCulture), Convert.ToInt32(explicitValue, CultureInfo.InvariantCulture));
    }

    private static int? GetSerializeOrder(ISymbol symbol, INamedTypeSymbol? serializeOrderAttribute)
    {
        AttributeData? attribute = symbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serializeOrderAttribute));

        if (attribute?.ConstructorArguments.Length != 1)
        {
            return null;
        }

        object? value = attribute.ConstructorArguments[0].Value;
        return value switch
        {
            int intValue => intValue,
            byte byteValue => byteValue,
            short shortValue => shortValue,
            _ => null
        };
    }

    private static int? GetMiddlewareOrder(ITypeSymbol typeSymbol, INamedTypeSymbol? middlewareOrderAttribute)
    {
        AttributeData? attribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, middlewareOrderAttribute));

        if (attribute?.ConstructorArguments.Length != 1)
        {
            return null;
        }

        object? value = attribute.ConstructorArguments[0].Value;
        return value switch
        {
            int intValue => intValue,
            short shortValue => shortValue,
            byte byteValue => byteValue,
            sbyte sbyteValue => sbyteValue,
            _ => null
        };
    }

    private static bool IsFixedSizeSerializationType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (typeSymbol is IArrayTypeSymbol)
        {
            return false;
        }

        return typeSymbol.IsValueType || typeSymbol.SpecialType is not SpecialType.None;
    }

    private static bool IsSupportedConfigurationType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (typeSymbol.SpecialType is SpecialType.System_Char
            or SpecialType.System_String
            or SpecialType.System_Boolean
            or SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_DateTime)
        {
            return true;
        }

        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is "global::System.Guid" or "global::System.TimeSpan";
    }

    private static bool TryGetConstantInt(IOperation operation, out int value)
    {
        object? constantValue = operation.ConstantValue.HasValue ? operation.ConstantValue.Value : null;
        switch (constantValue)
        {
            case int intValue:
                value = intValue;
                return true;
            case short shortValue:
                value = shortValue;
                return true;
            case sbyte sbyteValue:
                value = sbyteValue;
                return true;
            case byte byteValue:
                value = byteValue;
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool IsNullLiteral(IOperation operation)
        => operation.ConstantValue.HasValue && operation.ConstantValue.Value is null;

    private static ITypeSymbol? GetUnderlyingType(IOperation operation)
    {
        IOperation current = operation;
        while (current is IConversionOperation conversion)
        {
            current = conversion.Operand;
        }

        return current.Type ?? operation.Type;
    }

    private static bool? TryGetEncryptValue(IOperation operation, SymbolSet symbols)
    {
        if (operation.Type is null || !SymbolEqualityComparer.Default.Equals(operation.Type, symbols.RequestOptionsType))
        {
            return null;
        }

        if (operation is IPropertyReferenceOperation propertyReference
            && propertyReference.Property.Name == "Default"
            && SymbolEqualityComparer.Default.Equals(propertyReference.Member.ContainingType, symbols.RequestOptionsType))
        {
            return false;
        }

        if (operation is IObjectCreationOperation creation
            && SymbolEqualityComparer.Default.Equals(creation.Type, symbols.RequestOptionsType))
        {
            foreach (IPropertyReferenceOperation initializer in creation.Initializer?.Initializers.OfType<ISimpleAssignmentOperation>()
                         .Select(static assignment => assignment.Target)
                         .OfType<IPropertyReferenceOperation>() ?? [])
            {
                if (initializer.Property.Name == "Encrypt")
                {
                    ISimpleAssignmentOperation assignment = (ISimpleAssignmentOperation)initializer.Parent!;
                    if (assignment.Value.ConstantValue is { HasValue: true, Value: bool boolValue })
                    {
                        return boolValue;
                    }
                }
            }

            return false;
        }

        if (operation is IInvocationOperation invocation
            && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, symbols.RequestOptionsType))
        {
            string methodName = invocation.TargetMethod.Name;
            if (methodName == "WithEncrypt")
            {
                if (invocation.Arguments.Length == 0)
                {
                    return true;
                }

                return invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: bool boolValue }
                    ? boolValue
                    : null;
            }

            if (methodName is "WithRetry" or "WithTimeout")
            {
                return TryGetEncryptValue(invocation.Instance!, symbols);
            }
        }

        return null;
    }

    private static bool IsBuilderOpcodeAccess(ExpressionSyntax expression, string builderParameterName)
        => expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: var identifier },
            Name.Identifier.ValueText: "Opcode"
        } && identifier == builderParameterName;

    private static bool IsInboundAlwaysExecute(AttributeData stageAttribute, INamedTypeSymbol? middlewareStageType)
    {
        if (stageAttribute.ConstructorArguments.Length != 1
            || stageAttribute.ConstructorArguments[0].Type is null
            || !SymbolEqualityComparer.Default.Equals(stageAttribute.ConstructorArguments[0].Type, middlewareStageType)
            || stageAttribute.ConstructorArguments[0].Value is not byte stageValue
            || stageValue != 0)
        {
            return false;
        }

        foreach (KeyValuePair<string, TypedConstant> namedArgument in stageAttribute.NamedArguments)
        {
            if (namedArgument.Key == "AlwaysExecute"
                && namedArgument.Value.Value is bool alwaysExecute
                && alwaysExecute)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOpcodeGuard(SyntaxNode node, string builderParameterName)
    {
        if (node is BinaryExpressionSyntax binary)
        {
            return (binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.EqualsExpression)
                    || binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NotEqualsExpression))
                   && (IsBuilderOpcodeAccess(binary.Left, builderParameterName) || IsBuilderOpcodeAccess(binary.Right, builderParameterName));
        }

        if (node is IsPatternExpressionSyntax pattern)
        {
            return IsBuilderOpcodeAccess(pattern.Expression, builderParameterName);
        }

        return false;
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, IMethodSymbol methodSymbol, params object[] messageArgs)
    {
        Location? location = methodSymbol.Locations.FirstOrDefault(static l => l.IsInSource);
        if (location is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
        }
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, ISymbol symbol, params object[] messageArgs)
    {
        Location? location = symbol.Locations.FirstOrDefault(static l => l.IsInSource);
        if (location is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
        }
    }

    private static void Report(OperationAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] messageArgs)
        => context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));

    private static void Report(OperationAnalysisContext context, DiagnosticDescriptor descriptor, SyntaxNode syntax, params object[] messageArgs)
    {
        Location location = syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, SymbolSet symbols)
    {
        IMethodSymbol method = (IMethodSymbol)context.Symbol;
        AnalyzePacketOpcodeOnNonController(context, method, symbols);
        AnalyzeOpCodeDocumentation(context, method, symbols);
        AnalyzeBufferLeaseLeak(context, method, symbols);
    }

    private static void AnalyzePacketOpcodeOnNonController(SymbolAnalysisContext context, IMethodSymbol method, SymbolSet symbols)
    {
        if (!HasAttribute(method, symbols.PacketOpcodeAttribute))
        {
            return;
        }

        if (method.ContainingType is null || HasAttribute(method.ContainingType, symbols.ControllerAttribute))
        {
            return;
        }

        Report(
            context,
            DiagnosticDescriptors.PacketOpcodeOnNonControllerType,
            method,
            method.Name,
            method.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, SymbolSet symbols)
    {
        IObjectCreationOperation creation = (IObjectCreationOperation)context.Operation;
        if (creation.IsImplicit)
        {
            return;
        }

        if (context.ContainingSymbol is not IMethodSymbol method)
        {
            return;
        }

        if (IsNalixHotPath(method, symbols))
        {
            ITypeSymbol? type = creation.Type;
            if (type is null)
            {
                return;
            }

            // Warn for reference types that are not explicitly allowed in hot paths
            if (type.IsReferenceType && !IsAllowedInHotPath(type))
            {
                Report(context, DiagnosticDescriptors.AllocationInHotPath, creation.Syntax, method.Name, type.Name);
            }
        }
    }

    private static bool IsAllowedInHotPath(ITypeSymbol type)
    {
        if (type.IsValueType)
        {
            return true;
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            return true;
        }

        string name = type.Name;
        return name.EndsWith("Exception") || name == "CancellationTokenSource" || name == "StringBuilder";
    }

    private static bool IsNalixHotPath(IMethodSymbol method, SymbolSet symbols)
    {
        if (HasAttribute(method, symbols.PacketOpcodeAttribute))
        {
            return true;
        }

        if (method.Name == "InvokeAsync" && ImplementsOpenGeneric(method.ContainingType, symbols.PacketMiddlewareType))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsInvocation(IOperation? operation, string methodName)
    {
        while (operation is not null)
        {
            if (operation is IInvocationOperation invocation)
            {
                if (invocation.TargetMethod.Name == methodName)
                {
                    return true;
                }

                operation = invocation.Instance;
                continue;
            }

            if (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
                continue;
            }

            break;
        }

        return false;
    }

    private static void AnalyzeOpCodeDocumentation(SymbolAnalysisContext context, IMethodSymbol method, SymbolSet symbols)
    {
        ushort? opcode = GetOpcode(method, symbols.PacketOpcodeAttribute);
        if (!opcode.HasValue)
        {
            return;
        }

        string? xml = method.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(xml))
        {
            return;
        }

        // Look for 0xXXXX patterns
        Match match = Regex.Match(xml, @"0x([0-9A-Fa-f]{1,4})");
        if (match.Success)
        {
            if (ushort.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, null, out ushort docOpcode))
            {
                if (docOpcode != opcode.Value)
                {
                    Report(context, DiagnosticDescriptors.OpCodeDocMismatch, method, method.Name, docOpcode, opcode.Value);
                }
            }
        }
    }

    private static void AnalyzeBufferLeaseLeak(SymbolAnalysisContext context, IMethodSymbol method, SymbolSet symbols)
    {
        if (method.MethodKind != MethodKind.Ordinary || (method.ContainingType.TypeKind == TypeKind.Interface && method.IsAbstract))
        {
            return;
        }

        List<IParameterSymbol> leaseParams = [.. method.Parameters.Where(p => SymbolEqualityComparer.Default.Equals(p.Type, symbols.BufferLeaseType))];
        if (leaseParams.Count == 0)
        {
            return;
        }

        // Skip if the method itself is marked as [Pure]
        if (method.GetAttributes().Any(a => a.AttributeClass?.Name == "PureAttribute"))
        {
            return;
        }

        foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
        {
            SyntaxNode syntax = reference.GetSyntax(context.CancellationToken);
            string code = syntax.ToString();

            // Look for disposal or return via next delegate
            // This is still a heuristic but we'll make it slightly more specific
            foreach (IParameterSymbol p in leaseParams)
            {
                // Skip parameters explicitly marked as borrowed/no-dispose, or 'out' parameters 
                // where ownership is transferred to the caller.
                if (p.GetAttributes().Any(a => a.AttributeClass?.Name is "BorrowedAttribute" or "NoDisposeAttribute") || p.RefKind == RefKind.Out)
                {
                    continue;
                }

                string name = p.Name;
                // Heuristic: check if the lease is disposed, used in a using block, 
                // passed to a next delegate, or returned in any form.
                bool isDisposed = code.Contains($"{name}.Dispose()")
                                || code.Contains($"using (var {name}")
                                || code.Contains($"using var {name}")
                                || Regex.IsMatch(code, $@"\b(next|nextHandler|ExecuteAsync)\s*\([^)]*\b{name}\b")
                                || Regex.IsMatch(code, $@"return\s+[^;]*\b{name}\b")
                                || Regex.IsMatch(code, $@"=> \s*[^;]*\b{name}\b");

                if (!isDisposed)
                {
                    Report(context, DiagnosticDescriptors.PotentialBufferLeaseLeak, p, name);
                }
            }
        }
    }
}
