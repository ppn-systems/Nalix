// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
            DiagnosticDescriptors.NetworkHostingUdpWithoutTcpBinding
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

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, symbols, globalOpcodes),
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

    private static void AnalyzeNamedType(SymbolAnalysisContext context, SymbolSet symbols, ConcurrentDictionary<ushort, (IMethodSymbol Method, INamedTypeSymbol Controller)> globalOpcodes)
    {
        INamedTypeSymbol typeSymbol = (INamedTypeSymbol)context.Symbol;

        if (typeSymbol.TypeKind is not (TypeKind.Class or TypeKind.Struct) || typeSymbol.IsAbstract || typeSymbol.IsImplicitlyDeclared)
        {
            return;
        }

        if (HasAttribute(typeSymbol, symbols.ControllerAttribute))
        {
            AnalyzeControllerType(context, typeSymbol, symbols, globalOpcodes);
        }

        AnalyzePacketType(context, typeSymbol, symbols);
        AnalyzeSerializationType(context, typeSymbol, symbols);
        AnalyzeConfigurationType(context, typeSymbol, symbols);
        AnalyzeMetadataProviderType(context, typeSymbol, symbols);
        AnalyzeMiddlewareType(context, typeSymbol, symbols);

        if (Implements(typeSymbol, symbols.NetworkBufferMiddlewareType) && HasAttribute(typeSymbol, symbols.MiddlewareStageAttribute))
        {
            Location? location = typeSymbol.Locations.FirstOrDefault(static l => l.IsInSource);
            if (location is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.BufferMiddlewareShouldNotUseStageAttribute,
                    location,
                    typeSymbol.Name));
            }
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
        bool isPacketMiddleware = ImplementsOpenGeneric(typeSymbol, symbols.PacketMiddlewareType);
        bool isBufferMiddleware = Implements(typeSymbol, symbols.NetworkBufferMiddlewareType);

        if (!isPacketMiddleware && !isBufferMiddleware)
        {
            return;
        }

        AttributeData? orderAttribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.MiddlewareOrderAttribute));
        AttributeData? stageAttribute = typeSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.MiddlewareStageAttribute));

        if (isPacketMiddleware && orderAttribute is null)
        {
            Report(context, DiagnosticDescriptors.PacketMiddlewareMissingOrder, typeSymbol, typeSymbol.Name);
        }

        if (isBufferMiddleware)
        {
            if (orderAttribute is null)
            {
                Report(context, DiagnosticDescriptors.BufferMiddlewareMissingOrder, typeSymbol, typeSymbol.Name);
            }

            if (stageAttribute is not null)
            {
                return;
            }
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
        List<(ISymbol Member, int Order)> orderedMembers = [];

        foreach (ISymbol member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol and not IFieldSymbol)
            {
                continue;
            }

            if (member.IsImplicitlyDeclared)
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

                if (finalOrder.Value < 0)
                {
                    Report(context, DiagnosticDescriptors.NegativeSerializeOrder, member, member.Name, finalOrder.Value);
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
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context, SymbolSet symbols)
    {
        MethodDeclarationSyntax methodDeclaration = (MethodDeclarationSyntax)context.Node;
        if (methodDeclaration.Identifier.ValueText != "ResetForPool"
            || methodDeclaration.ParameterList.Parameters.Count != 0)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!methodSymbol.IsOverride || methodSymbol.ContainingType is not INamedTypeSymbol containingType || !InheritsPacketBase(containingType, symbols))
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

    private static void AnalyzePacketType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols)
    {
        INamedTypeSymbol? baseType = typeSymbol.BaseType;
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
    }

    private static void AnalyzeControllerType(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, SymbolSet symbols, ConcurrentDictionary<ushort, (IMethodSymbol Method, INamedTypeSymbol Controller)> globalOpcodes)
    {
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
                if (opcode.Value < 0x0100 && !HasInternalByPass(methodSymbol, typeSymbol, symbols))
                {
                    Report(context, DiagnosticDescriptors.ReservedOpCodeRange, methodSymbol, methodSymbol.Name, opcode.Value);
                }

                if (globalOpcodes.TryGetValue(opcode.Value, out (IMethodSymbol Method, INamedTypeSymbol Controller) existing) && !SymbolEqualityComparer.Default.Equals(existing.Controller, typeSymbol))
                {
                    Report(context, DiagnosticDescriptors.GlobalDuplicateOpcode, methodSymbol, methodSymbol.Name, opcode.Value, existing.Method.Name, existing.Controller.Name);
                }
                else
                {
                    globalOpcodes[opcode.Value] = (methodSymbol, typeSymbol);
                }

                if (!HasSupportedSignature(methodSymbol, symbols))
                {
                    Report(context, DiagnosticDescriptors.InvalidControllerHandlerSignature, methodSymbol, methodSymbol.Name);
                }
                else
                {
                    ReportPacketContextMismatchIfAny(context, methodSymbol, symbols);
                }
            }
            else if (isCandidate)
            {
                Report(context, DiagnosticDescriptors.ControllerMethodRequiresOpcode, methodSymbol, methodSymbol.Name);
            }
        }
    }

    private static bool HasSupportedSignature(IMethodSymbol methodSymbol, SymbolSet symbols)
    {
        if (!HasSupportedReturnType(methodSymbol.ReturnType, symbols))
        {
            return false;
        }

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

        if (returnType is INamedTypeSymbol namedReturnType && namedReturnType.IsGenericType)
        {
            return SymbolEqualityComparer.Default.Equals(namedReturnType.ConstructedFrom, symbols.GenericTaskType)
                || SymbolEqualityComparer.Default.Equals(namedReturnType.ConstructedFrom, symbols.GenericValueTaskType);
        }

        return false;
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

        return attribute.ConstructorArguments[0].Value is ushort opcode ? opcode : null;
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
        return arg.Type is not null
               && SymbolEqualityComparer.Default.Equals(arg.Type, serializeLayoutType)
               && arg.Value is byte rawValue
               && rawValue == 1;
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
        AnalyzeOpCodeDocumentation(context, method, symbols);
        AnalyzeBufferLeaseLeak(context, method, symbols);
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

        if (method.Name == "InvokeAsync" && (ImplementsOpenGeneric(method.ContainingType, symbols.PacketMiddlewareType) || Implements(method.ContainingType, symbols.NetworkBufferMiddlewareType)))
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
